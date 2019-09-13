﻿/*******************************************************}
{                                                       }
{         基于HCView的电子病历程序  作者：荆通          }
{                                                       }
{ 此代码仅做学习交流使用，不可用于商业目的，由此引发的  }
{ 后果请使用者承担，加入QQ群 649023932 来获取更多的技术 }
{ 交流。                                                }
{                                                       }
{*******************************************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HC.View;
using System.IO;
using HC.Win32;
using System.Drawing;

namespace EMRView
{
    public delegate void SyncDeItemEventHandle(object sender, HCCustomData aData, HCCustomItem aItem);
    public delegate void SyncDeFloatItemEventHandler(object sender, HCSectionData aData, HCCustomFloatItem aItem);
    public delegate bool HCCopyPasteStreamEventHandler(Stream aStream);
    public class HCEmrView : HCEmrViewIH
    {
        private bool FDesignMode;
        private bool FHideTrace;  // 隐藏痕迹
        private bool FTrace;  // 是否处于留痕迹状态
        private int FTraceCount;  // 当前文档痕迹数量
        private Color FDeDoneColor, FDeUnDoneColor;
        private string FPageBlankTip;
        private EventHandler FOnCanNotEdit;
        private SyncDeItemEventHandle FOnSyncDeItem;
        private SyncDeFloatItemEventHandler FOnSyncDeFloatItem;
        private HCCopyPasteEventHandler FOnCopyRequest, FOnPasteRequest;
        private HCCopyPasteStreamEventHandler FOnCopyAsStream, FOnPasteFromStream;

        private void SetPageBlankTip(string value)
        {
            if (FPageBlankTip != value)
            {
                FPageBlankTip = value;
                this.UpdateView();
            }
        }

        private void DoSyncDeItem(object sender, HCCustomData aData, HCCustomItem aItem)
        {
            if (FOnSyncDeItem != null)
                FOnSyncDeItem(sender, aData, aItem);
        }

        private void DoSyncDeFloatItem(object sender, HCSectionData aData, HCCustomFloatItem aItem)
        {
            if (FOnSyncDeFloatItem != null)
                FOnSyncDeFloatItem(sender, aData, aItem);
        }

        private void DoDeItemPaintBKG(object sender, HCCanvas aCanvas, RECT aDrawRect, PaintInfo aPaintInfo)
        {
            if (aPaintInfo.Print)
                return;
            
            DeItem vDeItem = sender as DeItem;
            if (!vDeItem.Selected())
            {
                if (vDeItem.IsElement)
                {
                    if (vDeItem.OutOfRang)
                    {
                        aCanvas.Brush.Color = Color.Red;
                        aCanvas.FillRect(aDrawRect);
                    }
                    else
                    if (vDeItem.MouseIn || vDeItem.Active)  // 鼠标移入和光标在其中
                    {
                        if (vDeItem[DeProp.Name] != vDeItem.Text)  // 已经填写过了
                            aCanvas.Brush.Color = FDeDoneColor;
                        else  // 没填写过
                            aCanvas.Brush.Color = FDeUnDoneColor;

                        aCanvas.FillRect(aDrawRect);
                    }
                    else  // 静态
                    if (FDesignMode)  // 设计模式
                    {
                        aCanvas.Brush.Color = HC.View.HC.clBtnFace;
                        aCanvas.FillRect(aDrawRect);
                    }
                }
                else  // 不是数据元
                if (FDesignMode || vDeItem.MouseIn || vDeItem.Active)
                {
                    if (vDeItem.EditProtect || vDeItem.CopyProtect)
                    {
                        aCanvas.Brush.Color = HC.View.HC.clBtnFace;
                        aCanvas.FillRect(aDrawRect);
                    }
                }
            }

            if (!FHideTrace)  // 显示痕迹
            {
                if (vDeItem.StyleEx == StyleExtra.cseDel)  // 痕迹
                {
                    int vTextHeight = Style.TextStyles[vDeItem.StyleNo].FontHeight;
                    int vAlignVert = User.DT_BOTTOM;
                    switch (Style.ParaStyles[vDeItem.ParaNo].AlignVert)
                    {
                        case ParaAlignVert.pavCenter:
                            vAlignVert = User.DT_CENTER;
                            break;

                        case ParaAlignVert.pavTop:
                            vAlignVert = User.DT_TOP;
                            break;

                        default:
                            vAlignVert = User.DT_BOTTOM;
                            break;
                    }

                    int vTop = aDrawRect.Top;
                    switch (vAlignVert)
                    {
                        case User.DT_TOP:
                            vTop = aDrawRect.Top;
                            break;

                        case User.DT_CENTER:
                            vTop = aDrawRect.Top + (aDrawRect.Bottom - aDrawRect.Top - vTextHeight) / 2;
                            break;

                        default:
                            vTop = aDrawRect.Bottom - vTextHeight;
                            break;
                    }

                    // 绘制删除线
                    aCanvas.Pen.BeginUpdate();
                    try
                    {
                        aCanvas.Pen.Style = HCPenStyle.psSolid;
                        aCanvas.Pen.Color = Color.Red;
                    }
                    finally
                    {
                        aCanvas.Pen.EndUpdate();
                    }

                    vTop = vTop + (aDrawRect.Bottom - vTop) / 2;
                    aCanvas.MoveTo(aDrawRect.Left, vTop - 1);
                    aCanvas.LineTo(aDrawRect.Right, vTop - 1);
                    aCanvas.MoveTo(aDrawRect.Left, vTop + 2);
                    aCanvas.LineTo(aDrawRect.Right, vTop + 2);
                }
                else
                if (vDeItem.StyleEx == StyleExtra.cseAdd)
                {
                    aCanvas.Pen.BeginUpdate();
                    try
                    {
                        aCanvas.Pen.Style = HCPenStyle.psSolid;
                        aCanvas.Pen.Color = Color.Blue;
                    }
                    finally
                    {
                        aCanvas.Pen.EndUpdate();
                    }

                    aCanvas.MoveTo(aDrawRect.Left, aDrawRect.Bottom);
                    aCanvas.LineTo(aDrawRect.Right, aDrawRect.Bottom);
                }
            }
        }

        private void InsertEmrTraceItem(string aText)
        {
            DeItem vEmrTraceItem = new DeItem(aText);

            if (this.CurStyleNo < HCStyle.Null)
                vEmrTraceItem.StyleNo = 0;
            else
                vEmrTraceItem.StyleNo = this.CurStyleNo;

            vEmrTraceItem.ParaNo = this.CurParaNo;
            vEmrTraceItem.StyleEx = StyleExtra.cseAdd;

            this.InsertItem(vEmrTraceItem);
        }

        private bool CanNotEdit()
        {
            bool Result = (!this.ActiveSection.ActiveData.CanEdit()) 
                || (!(this.ActiveSectionTopLevelData() as HCRichData).CanEdit());

            if (Result && (FOnCanNotEdit != null))
                FOnCanNotEdit(this, null);

            return Result;
        }

        protected override HCCustomFloatItem DoSectionCreateFloatStyleItem(HCSectionData aData, int aStyleNo)
        {
            return HCEmrViewLite.CreateEmrFloatStyleItem(aData, aStyleNo);
        }

        protected override void DoSectionInsertFloatItem(object sender, HCSectionData aData, HCCustomFloatItem aItem)
        {
            if (aItem is DeFloatBarCodeItem)
                DoSyncDeFloatItem(sender, aData, aItem);

            base.DoSectionInsertFloatItem(sender, aData, aItem);
        }

        /// <summary> 当有新Item创建完成后触发的事件 </summary>
        /// <param name="sender">Item所属的文档节</param>
        /// <param name="e"></param>
        protected override void DoSectionCreateItem(object sender, EventArgs e)
        {
            if ((!Style.States.Contain(HCState.hosLoading)) && FTrace)
                (sender as DeItem).StyleEx = StyleExtra.cseAdd;

            base.DoSectionCreateItem(sender, e);
        }

        /// <summary> 当有新Item创建时触发 </summary>
        /// <param name="aData">创建Item的Data</param>
        /// <param name="aStyleNo">要创建的Item样式</param>
        /// <returns>创建好的Item</returns>
        protected override HCCustomItem DoSectionCreateStyleItem(HCCustomData aData, int aStyleNo)
        {
            return HCEmrViewLite.CreateEmrStyleItem(aData, aStyleNo);
        }

        /// <summary> 当节某Data有Item插入后触发 </summary>
        /// <param name="sender">在哪个文档节插入</param>
        /// <param name="aData">在哪个Data插入</param>
        /// <param name="aItem">已插入的Item</param>
        protected override void DoSectionInsertItem(object sender, HCCustomData aData, HCCustomItem aItem)
        {
            if (aItem is DeItem)
            {
                DeItem vDeItem = aItem as DeItem;
                vDeItem.OnPaintBKG = DoDeItemPaintBKG;

                //if (aData.Style.States.Contain(HCState.hosPasting))
                //    DoPasteItem();

                if (vDeItem.StyleEx != StyleExtra.cseNone)
                {
                    FTraceCount++;

                    if (!this.AnnotatePre.Visible)
                        this.AnnotatePre.Visible = true;
                }

                DoSyncDeItem(sender, aData, aItem);
            }
            else
            if (aItem is DeEdit)
                DoSyncDeItem(sender, aData, aItem);
            else
            if (aItem is DeCombobox)
                DoSyncDeItem(sender, aData, aItem);

            base.DoSectionInsertItem(sender, aData, aItem);
        }

        /// <summary> 当节中某Data有Item删除后触发 </summary>
        /// <param name="sender">在哪个文档节删除</param>
        /// <param name="aData">在哪个Data删除</param>
        /// <param name="aItem">已删除的Item</param>
        protected override void DoSectionRemoveItem(object sender, HCCustomData aData, HCCustomItem aItem)
        {
            if (aItem is DeItem)
            {
                DeItem vDeItem = aItem as DeItem;
                vDeItem.OnPaintBKG = DoDeItemPaintBKG;

                if (vDeItem.StyleEx != StyleExtra.cseNone)
                {
                    FTraceCount--;

                    if ((FTraceCount == 0) && (this.AnnotatePre.Visible) && (this.AnnotatePre.Count == 0))
                        this.AnnotatePre.Visible = false;
                }
            }

            base.DoSectionRemoveItem(sender, aData, aItem);
        }

        protected override bool DoSectionSaveItem(object sender, HCCustomData aData, HCCustomItem aItem)
        {
            bool vResult = base.DoSectionSaveItem(sender, aData, aItem);
            if (Style.States.Contain(HCState.hosCopying))  // 非设计模式、复制保存
            {
                if ((aItem is DeGroup) && (!FDesignMode))
                    vResult = false;
                else
                if (aItem is DeItem)
                    vResult = !(aItem as DeItem).CopyProtect;  // 是否禁止复制
            }

            return vResult;
        }

        /// <summary> 指定的节当前是否可编辑 </summary>
        /// <param name="sender">文档节</param>
        /// <returns>True：可编辑，False：不可编辑</returns>
        protected override bool DoSectionCanEdit(Object sender)
        {
            HCViewData vViewData = sender as HCViewData;
            if ((vViewData.ActiveDomain != null) && (vViewData.ActiveDomain.BeginNo >= 0))
                return !((vViewData.Items[vViewData.ActiveDomain.BeginNo] as DeGroup).ReadOnly);
            else
                return true;
        }

        protected override bool DoSectionDeleteItem(object sender, HCCustomData aData, HCCustomItem aItem)
        {
            bool vResult = base.DoSectionDeleteItem(sender, aData, aItem);
            if ((aItem is DeGroup) && (!FDesignMode))  // 设计模式不允许删除数据组
                vResult = false;

            return vResult;
        }

        /// <summary> 按键按下 </summary>
        /// <param name="e">按键信息</param>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (FTrace)
            {
                if (HC.View.HC.IsKeyDownEdit(e.KeyValue))
                {
                    if (CanNotEdit())
                        return;

                    string vText = "";
                    string vCurTrace = "";
                    int vStyleNo = HCStyle.Null;
                    int vParaNo = HCStyle.Null;
                    StyleExtra vCurStyleEx = StyleExtra.cseNone;

                    HCRichData vData = this.ActiveSectionTopLevelData() as HCRichData;
                    if (vData.SelectExists())
                    {
                        this.DisSelect();
                        return;
                    }

                    if (vData.SelectInfo.StartItemNo < 0)
                        return;

                    if (vData.Items[vData.SelectInfo.StartItemNo].StyleNo < HCStyle.Null)
                    {
                        if (vData.SelectInfo.StartItemOffset == HC.View.HC.OffsetBefor)  // 在最前面
                        {
                            if (e.KeyCode == Keys.Back)  // 回删
                            {
                                if (vData.SelectInfo.StartItemNo == 0)
                                    return;  // 第一个最前面则不处理
                                else  // 不是第一个最前面
                                {
                                    vData.SelectInfo.StartItemNo = vData.SelectInfo.StartItemNo - 1;
                                    vData.SelectInfo.StartItemOffset = vData.Items[vData.SelectInfo.StartItemNo].Length;
                                    this.OnKeyDown(e);
                                }
                            }
                            else
                            if (e.KeyCode == Keys.Delete)  // 后删
                            {
                                vData.SelectInfo.StartItemOffset = HC.View.HC.OffsetAfter;
                                //this.OnKeyDown(e);
                            }
                            else
                                base.OnKeyDown(e);
                        }
                        else
                        if (vData.SelectInfo.StartItemOffset == HC.View.HC.OffsetAfter)  // 在最后面
                        {
                            if (e.KeyCode == Keys.Back)
                            {
                                vData.SelectInfo.StartItemOffset = HC.View.HC.OffsetBefor;
                                //this.OnKeyDown(e);
                            }
                            else
                            if (e.KeyCode == Keys.Delete)
                            {
                                if (vData.SelectInfo.StartItemNo == vData.Items.Count - 1)
                                    return;
                                else
                                {
                                    vData.SelectInfo.StartItemNo = vData.SelectInfo.StartItemNo + 1;
                                    vData.SelectInfo.StartItemOffset = 0;
                                    this.OnKeyDown(e);
                                }
                            }
                            else
                                base.OnKeyDown(e);
                        }
                        else
                            base.OnKeyDown(e);

                        return;
                    }

                    // 取光标处的文本
                    if (e.KeyCode == Keys.Back)  // 回删
                    {
                        if ((vData.SelectInfo.StartItemNo == 0)
                            && (vData.SelectInfo.StartItemOffset == 0))  // 第一个最前面则不处理
                            return;
                        else  // 不是第一个最前面
                        if (vData.SelectInfo.StartItemOffset == 0)  // 最前面，移动到前一个最后面处理
                        {
                            if (vData.Items[vData.SelectInfo.StartItemNo].Text != "")  // 当前行不是空行
                            {
                                vData.SelectInfo.StartItemNo = vData.SelectInfo.StartItemNo - 1;
                                vData.SelectInfo.StartItemOffset = vData.Items[vData.SelectInfo.StartItemNo].Length;
                                this.OnKeyDown(e);
                            }
                            else  // 空行不留痕直接默认处理
                                base.OnKeyDown(e);

                            return;
                        }
                        else  // 不是第一个Item，也不是在Item最前面
                        if (vData.Items[vData.SelectInfo.StartItemNo] is DeItem)  // 文本
                        {
                            DeItem vDeItem = vData.Items[vData.SelectInfo.StartItemNo] as DeItem;
                            vText = vDeItem.SubString(vData.SelectInfo.StartItemOffset, 1);
                            vStyleNo = vDeItem.StyleNo;
                            vParaNo = vDeItem.ParaNo;
                            vCurStyleEx = vDeItem.StyleEx;
                            vCurTrace = vDeItem[DeProp.Trace];
                        }
                    }
                    else
                    if (e.KeyCode == Keys.Delete)  // 后删
                    {
                        if ((vData.SelectInfo.StartItemNo == vData.Items.Count - 1)
                            && (vData.SelectInfo.StartItemOffset == vData.Items[vData.Items.Count - 1].Length))
                            return;  // 最后一个最后面则不处理
                        else
                        if (vData.SelectInfo.StartItemOffset == vData.Items[vData.SelectInfo.StartItemNo].Length)  // 最后面，移动到后一个最前面处理
                        {
                            vData.SelectInfo.StartItemNo = vData.SelectInfo.StartItemNo + 1;
                            vData.SelectInfo.StartItemOffset = 0;
                            this.OnKeyDown(e);

                            return;
                        }
                        else  // 不是最后一个Item，也不是在Item最后面
                        if (vData.Items[vData.SelectInfo.StartItemNo] is DeItem)  // 文本
                        {
                            DeItem vDeItem = vData.Items[vData.SelectInfo.StartItemNo] as DeItem;
                            vText = vDeItem.SubString(vData.SelectInfo.StartItemOffset + 1, 1);
                            vStyleNo = vDeItem.StyleNo;
                            vParaNo = vDeItem.ParaNo;
                            vCurStyleEx = vDeItem.StyleEx;
                            vCurTrace = vDeItem[DeProp.Trace];
                        }
                    }

                    // 删除掉的内容以痕迹的形式插入
                    this.BeginUpdate();
                    try
                    {
                        base.OnKeyDown(e);

                        if (FTrace && (vText != "")) // 有删除的内容
                        {
                            if ((vCurStyleEx == StyleExtra.cseAdd) && (vCurTrace == ""))  // 新添加未生效痕迹可以直接删除
                                return;

                            // 创建删除字符对应的Item
                            DeItem vDeItem = new DeItem();
                            vDeItem.Text = vText;
                            vDeItem.StyleNo = vStyleNo;
                            vDeItem.ParaNo = vParaNo;

                            if ((vCurStyleEx == StyleExtra.cseDel) && (vCurTrace == "")) // 原来是删除未生效痕迹
                                vDeItem.StyleEx = StyleExtra.cseNone;  // 取消删除痕迹
                            else  // 生成删除痕迹
                                vDeItem.StyleEx = StyleExtra.cseDel;

                            // 插入删除痕迹Item
                            HCCustomItem vCurItem = vData.Items[vData.SelectInfo.StartItemNo];
                            if (vData.SelectInfo.StartItemOffset == 0)  // 在Item最前面
                            {
                                if (vDeItem.CanConcatItems(vCurItem))  // 可以合并
                                {
                                    vCurItem.Text = vDeItem.Text + vCurItem.Text;

                                    if (e.KeyCode == Keys.Delete)  // 后删
                                        vData.SelectInfo.StartItemOffset = vData.SelectInfo.StartItemOffset + 1;

                                    this.ActiveSection.ReFormatActiveItem();
                                }
                                else  // 不能合并
                                {
                                    vDeItem.ParaFirst = vCurItem.ParaFirst;
                                    vCurItem.ParaFirst = false;
                                    this.InsertItem(vDeItem);
                                    if (e.KeyCode == Keys.Back)  // 回删
                                        vData.SelectInfo.StartItemOffset = vData.SelectInfo.StartItemOffset - 1;
                                }
                            }
                            else
                            if (vData.SelectInfo.StartItemOffset == vCurItem.Length)  // 在Item最后面
                            {
                                if (vCurItem.CanConcatItems(vDeItem))  // 可以合并
                                {
                                    vCurItem.Text = vCurItem.Text + vDeItem.Text;

                                    if (e.KeyCode == Keys.Delete)  // 后删
                                        vData.SelectInfo.StartItemOffset = vData.SelectInfo.StartItemOffset + 1;

                                    this.ActiveSection.ReFormatActiveItem();
                                }
                                else  // 不可以合并
                                {
                                    this.InsertItem(vDeItem);
                                    if (e.KeyCode == Keys.Back)  // 回删
                                        vData.SelectInfo.StartItemOffset = vData.SelectInfo.StartItemOffset - 1;
                                }
                            }
                            else  // 在Item中间
                            {
                                this.InsertItem(vDeItem);
                                if (e.KeyCode == Keys.Back)  // 回删
                                    vData.SelectInfo.StartItemOffset = vData.SelectInfo.StartItemOffset - 1;
                            }
                        }
                    }
                    finally
                    {
                        this.EndUpdate();
                    }
                }
                else
                    base.OnKeyDown(e);
            }
            else
                base.OnKeyDown(e);
        }

        /// <summary> 按键按压 </summary>
        /// <param name="e">按键信息</param>
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (HC.View.HC.IsKeyPressWant(e))
            {
                if (CanNotEdit())
                    return;

                if (FTrace)
                {
                    HCCustomData vData = this.ActiveSectionTopLevelData();
                    if (vData.SelectInfo.StartItemNo < 0)
                        return;

                    if (vData.SelectExists())
                        this.DisSelect();
                    else
                        InsertEmrTraceItem(e.KeyChar.ToString());

                    return;
                }

                base.OnKeyPress(e);
            }
        }

        /// <summary> 在当前位置插入文本 </summary>
        /// <param name="AText">要插入的字符串(支持带#13#10的回车换行)</param>
        /// <returns>True：插入成功，False：插入失败</returns>
        protected override bool DoInsertText(string aText)
        {
            if (CanNotEdit())
                return false;

            if (FTrace)
            {
                InsertEmrTraceItem(aText);
                return true;
            }
            else
                return base.DoInsertText(aText);
        }

        /// <summary> 复制前，便于控制是否允许复制 </summary>
        protected override bool DoCopyRequest(int aFormat)
        {
            if (FOnCopyRequest != null)
                return FOnCopyRequest(aFormat);
            else
                return base.DoCopyRequest(aFormat);
        }

        /// <summary> 粘贴前，便于控制是否允许粘贴 </summary>
        protected override bool DoPasteRequest(int aFormat)
        {
            if (FOnPasteRequest != null)
                return FOnPasteRequest(aFormat);
            else
                return base.DoPasteRequest(aFormat);
        }

        /// <summary> 复制前，便于订制特征数据如内容来源 </summary>
        protected override void DoCopyAsStream(Stream aStream)
        {
            if (FOnCopyAsStream != null)
                FOnCopyAsStream(aStream);
            else
                base.DoCopyAsStream(aStream);
        }

        /// <summary> 复制前，便于订制特征数据如内容来源 </summary>
        protected override bool DoPasteFormatStream(Stream aStream)
        {
            if (FOnPasteFromStream != null)
                return FOnPasteFromStream(aStream);
            else
                return base.DoPasteFormatStream(aStream);
        }

        #region 子方法，绘制当前页以下空白的提示
        private void DrawBlankTip_(int aLeft, int aTop, int aRight, int aDataDrawBottom, HCCanvas aCanvas)
        {
            if (aTop + 14 <= aDataDrawBottom)
            {
                aCanvas.Font.Size = 12;
                aCanvas.TextOut(aLeft + ((aRight - aLeft) - aCanvas.TextWidth(FPageBlankTip)) / 2, aTop, FPageBlankTip);
            }
        }
        #endregion

        /// <summary> 文档某节的Item绘制完成 </summary>
        /// <param name="AData">当前绘制的Data</param>
        /// <param name="ADrawItemIndex">Item对应的DrawItem序号</param>
        /// <param name="ADrawRect">Item对应的绘制区域</param>
        /// <param name="ADataDrawLeft">Data绘制时的Left</param>
        /// <param name="ADataDrawBottom">Data绘制时的Bottom</param>
        /// <param name="ADataScreenTop">绘制时呈现Data的Top位置</param>
        /// <param name="ADataScreenBottom">绘制时呈现Data的Bottom位置</param>
        /// <param name="ACanvas">画布</param>
        /// <param name="APaintInfo">绘制时的其它信息</param>
        protected override void DoSectionDrawItemPaintAfter(Object sender, HCCustomData aData, int aItemNo, int aDrawItemNo, RECT aDrawRect, 
            int aDataDrawLeft, int aDataDrawRight, int aDataDrawBottom, int aDataScreenTop, int aDataScreenBottom, HCCanvas aCanvas, PaintInfo aPaintInfo)
        {
            if ((!FHideTrace) && (FTraceCount > 0))  // 显示痕迹且有痕迹
            {
                HCCustomItem vItem = aData.Items[aItemNo];
                if (vItem.StyleNo > HCStyle.Null)
                {
                    DeItem vDeItem = vItem as DeItem;
                    if (vDeItem.StyleEx != StyleExtra.cseNone)  // 添加批注
                    {
                        HCDrawAnnotateDynamic vDrawAnnotate = new HCDrawAnnotateDynamic();
                        vDrawAnnotate.DrawRect = aDrawRect;
                        vDrawAnnotate.Title = vDeItem.GetHint();
                        vDrawAnnotate.Text = aData.GetDrawItemText(aDrawItemNo);

                        this.AnnotatePre.AddDrawAnnotate(vDrawAnnotate);
                    }
                }
            }

            if ((FPageBlankTip != "") && (aData is HCPageData))
            {
                if (aDrawItemNo < aData.DrawItems.Count - 1)
                {
                    if (aData.Items[aData.DrawItems[aDrawItemNo + 1].ItemNo].PageBreak)
                        DrawBlankTip_(aDataDrawLeft, aDrawRect.Top + aDrawRect.Height + aData.GetLineBlankSpace(aDrawItemNo), aDataDrawRight, aDataDrawBottom, aCanvas);
                }
                else
                    DrawBlankTip_(aDataDrawLeft, aDrawRect.Top + aDrawRect.Height + aData.GetLineBlankSpace(aDrawItemNo), aDataDrawRight, aDataDrawBottom, aCanvas);
            }

            base.DoSectionDrawItemPaintAfter(sender, aData, aItemNo, aDrawItemNo, aDrawRect, aDataDrawLeft, aDataDrawRight,
                aDataDrawBottom, aDataScreenTop, aDataScreenBottom, aCanvas, aPaintInfo);
        }

        protected override void WndProc(ref Message Message)
        {
            base.WndProc(ref Message);
        }

        protected override void Create()
        {
            base.Create();
            FHideTrace = false;
            FTrace = false;
            FTraceCount = 0;
            FDesignMode = false;
            HCTextItem.HCDefaultTextItemClass = typeof(DeItem);
            HCDomainItem.HCDefaultDomainItemClass = typeof(DeGroup);
        }

        public HCEmrView() : base()
        {
            this.Width = 100;
            this.Height = 100;
            FDeDoneColor = HC.View.HC.clBtnFace;
            FDeUnDoneColor = Color.FromArgb(0xFF, 0xDD, 0x80);
            FPageBlankTip = "";// "--------本页以下空白--------";
        }

        ~HCEmrView()
        {

        }

        /// <summary> 遍历Item </summary>
        /// <param name="ATraverse">遍历时信息</param>
        public void TraverseItem(HCItemTraverse aTraverse)
        {
            if (aTraverse.Areas.Count == 0)
                return;

            for (int i = 0; i <= this.Sections.Count - 1; i++)
            {
                if (!aTraverse.Stop)
                {
                    if (aTraverse.Areas.Contains(SectionArea.saHeader))
                        this.Sections[i].Header.TraverseItem(aTraverse);

                    if ((!aTraverse.Stop) && (aTraverse.Areas.Contains(SectionArea.saPage)))
                        this.Sections[i].Page.TraverseItem(aTraverse);

                    if ((!aTraverse.Stop) && (aTraverse.Areas.Contains(SectionArea.saFooter)))
                        this.Sections[i].Footer.TraverseItem(aTraverse);
                }
            }
        }

        /// <summary> 插入数据组 </summary>
        /// <param name="ADeGroup">数据组信息</param>
        /// <returns>True：成功，False：失败</returns>
        public bool InsertDeGroup(DeGroup aDeGroup)
        {
            return this.InsertDomain(aDeGroup);
        }

        /// <summary> 插入数据元 </summary>
        /// <param name="ADeItem">数据元信息</param>
        /// <returns>True：成功，False：失败</returns>
        public bool InsertDeItem(DeItem aDeItem)
        {
            return this.InsertItem(aDeItem);
        }

        /// <summary> 新建数据元 </summary>
        /// <param name="aText">数据元文本</param>
        /// <returns>新建好的数据元</returns>
        public DeItem NewDeItem(string aText)
        {
            DeItem Result = new DeItem();
            Result.Text = aText;

            if (this.CurStyleNo > HCStyle.Null)
                Result.StyleNo = this.CurStyleNo;
            else
                Result.StyleNo = 0;

            Result.ParaNo = this.CurParaNo;

            return Result;
        }

        /// <summary> 直接设置当前数据元的值为扩展内容 </summary>
		/// <param name="aStream">扩展内容流</param>
        public void SetActiveItemExtra(Stream aStream)
        {
            string vFileFormat = "";
            ushort vFileVersion = 0;
            byte vLang = 0;
            HC.View.HC._LoadFileFormatAndVersion(aStream, ref vFileFormat, ref vFileVersion, ref vLang);
            HCStyle vStyle = new HCStyle();
            try
            {
                vStyle.LoadFromStream(aStream, vFileVersion);
                this.BeginUpdate();
                try
                {
                    this.UndoGroupBegin();
                    try
                    {
                        HCRichData vTopData = this.ActiveSectionTopLevelData() as HCRichData;
                        this.DeleteActiveDataItems(vTopData.SelectInfo.StartItemNo);
                        ActiveSection.InsertStream(aStream, vStyle, vFileVersion);
                    }
                    finally
                    {
                        this.UndoGroupEnd();
                    }
                }
                finally
                {
                    this.EndUpdate();
                }
            }
            finally
            {
                vStyle.Dispose();
            }
        }

        /// <summary> 获取指定数据组中的文本内容 </summary>
        /// <param name="AData">指定从哪个Data里获取</param>
        /// <param name="ADeGroupStartNo">指定数据组的起始ItemNo</param>
        /// <param name="ADeGroupEndNo">指定数据组的结束ItemNo</param>
        /// <returns>数据组文本内容</returns>
        public string GetDataDeGroupText(HCViewData aData, int aDeGroupStartNo, int aDeGroupEndNo)
        {
            string Result = "";
            for (int i = aDeGroupStartNo + 1; i <= aDeGroupEndNo - 1; i++)
                Result = Result + aData.Items[i].Text;

            return Result;
        }

        /// <summary> 从当前数据组起始位置往前找相同数据组的内容Index域内容 </summary>
        /// <param name="AData">指定从哪个Data里获取</param>
        /// <param name="ADeGroupStartNo">指定从哪个位置开始往前找</param>
        /// <returns>相同数据组文本形式的内容</returns>
        public string GetDataForwardDeGroupText(HCViewData aData, int aDeGroupStartNo)
        {
            string Result = "";

            DeGroup vDeGroup = null;
            int vBeginNo = -1;
            int vEndNo = -1;
            string vDeIndex = (aData.Items[aDeGroupStartNo] as DeGroup)[DeProp.Index];

            for (int i = 0; i <= aDeGroupStartNo - 1; i++)  // 找起始
            {
                if (aData.Items[i] is DeGroup)
                {
                    vDeGroup = aData.Items[i] as DeGroup;
                    if (vDeGroup.MarkType == MarkType.cmtBeg)  // 是域起始
                    {
                        if (vDeGroup[DeProp.Index] == vDeIndex)  // 是目标域起始
                        {
                            vBeginNo = i;
                            break;
                        }
                    }
                }
            }

            if (vBeginNo >= 0)  // 找结束
            {
                for (int i = vBeginNo + 1; i <= aDeGroupStartNo - 1; i++)
                {
                    if (aData.Items[i] is DeGroup)
                    {
                        vDeGroup = aData.Items[i] as DeGroup;
                        if (vDeGroup.MarkType == MarkType.cmtEnd)  // 是域结束
                        {
                            if (vDeGroup[DeProp.Index] == vDeIndex)
                            {
                                vEndNo = i;
                                break;
                            }
                        }
                    }
                }

                if (vEndNo > 0)
                    Result = GetDataDeGroupText(aData, vBeginNo, vEndNo);
            }

            return Result;
        }

        /// <summary> 设置数据组的内容为指定的文本 </summary>
        /// <param name="aData">数据组所在的Data</param>
        /// <param name="aDeGroupNo">数据组的ItemNo</param>
        /// <param name="aText">文本内容</param>
        public void SetDeGroupText(HCViewData aData, int aDeGroupNo, string aText)
        {
            int vGroupBeg = -1;
            int vGroupEnd = aData.GetDomainAnother(aDeGroupNo);

            if (vGroupEnd > aDeGroupNo)
                vGroupBeg = aDeGroupNo;
            else
            {
                vGroupBeg = vGroupEnd;
                vGroupEnd = aDeGroupNo;
            }

            // 选中，使用插入时删除当前数据组中的内容
            aData.SetSelectBound(vGroupBeg, HC.View.HC.OffsetAfter, vGroupEnd, HC.View.HC.OffsetBefor);
            aData.InsertText(aText);
        }

        /// <summary> 文档是否处于设计模式 </summary>
        public bool DesignModeEx
        {
            get { return FDesignMode; }
            set { FDesignMode = value; }
        }

        /// <summary> 是否隐藏痕迹 </summary>
        public bool HideTrace
        {
            get { return FHideTrace; }
            set { FHideTrace = value; }
        }

        /// <summary> 是否处于留痕状态 </summary>
        public bool Trace
        {
            get { return FTrace; }
            set { FTrace = value; }
        }

        /// <summary> 文档中有几处痕迹 </summary>
        public int TraceCount
        {
            get { return FTraceCount; }
        }

        public string PageBlankTip
        {
            get { return FPageBlankTip; }
            set { SetPageBlankTip(value); }
        }

        /// <summary> 当编辑只读状态的Data时触发 </summary>
        public EventHandler OnCanNotEdit
        {
            get { return FOnCanNotEdit; }
            set { FOnCanNotEdit = value; }
        }

        /// <summary> 复制内容前触发 </summary>
        public HCCopyPasteEventHandler OnCopyRequest
        {
            get { return FOnCopyRequest; }
            set { FOnCopyRequest = value; }
        }

        /// <summary> 粘贴内容前触发 </summary>
        public HCCopyPasteEventHandler OnPasteRequest
        {
            get { return FOnPasteRequest; }
            set { FOnPasteRequest = value; }
        }

        public HCCopyPasteStreamEventHandler OnCopyAsStream
        {
            get { return FOnCopyAsStream; }
            set { FOnCopyAsStream = value; }
        }

        public HCCopyPasteStreamEventHandler OnPasteFromStream
        {
            get { return FOnPasteFromStream; }
            set { FOnPasteFromStream = value; }
        }

        /// <summary> 数据元需要同步内容时触发 </summary>
        public SyncDeItemEventHandle OnSyncDeItem
        {
            get { return FOnSyncDeItem; }
            set { FOnSyncDeItem = value; }
        }

        public SyncDeFloatItemEventHandler OnSyncDeFloatItem
        {
            get { return FOnSyncDeFloatItem; }
            set { FOnSyncDeFloatItem = value; }
        }
    }
}
