﻿/*******************************************************}
{                                                       }
{               HCView V1.1  作者：荆通                 }
{                                                       }
{      本代码遵循BSD协议，你可以加入QQ群 649023932      }
{            来获取更多的技术交流 2018-8-6              }
{                                                       }
{       文档ComboboxItem(下拉选择框)对象实现单元        }
{                                                       }
{*******************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HC.Win32;
using System.Drawing;
using System.IO;
using System.Xml;

namespace HC.View
{
    public class HCComScrollBar : HCScrollBar
    {

    }

    public class HCComboboxItem : HCEditItem
    {
        private bool FSaveItem;
        private List<string> FItems;
        private int FItemIndex, FMoveItemIndex;
        private RECT FButtonRect, FButtonDrawRect;
        private bool FMouseInButton;
        private HCPopupForm FPopupForm;
        private HCComScrollBar FScrollBar;
        private EventHandler FOnPopupItem;
        public static byte BTNWIDTH = 16;
        public static byte BTNMARGIN = 1;

        // DropDown部分
        private bool ScrollBarVisible()
        {
            return FItems.Count > DROPDOWNCOUNT;
        }

        private RECT GetItemRect()
        {
            if (ScrollBarVisible())
                return new RECT(0, 0, FPopupForm.Width - FScrollBar.Width, FPopupForm.Height);
            else
                return new RECT(0, 0, FPopupForm.Width, FPopupForm.Height);
        }

        private void DoScroll(Object Sender, ScrollCode ScrollCode, int ScrollPos)
        {
            FPopupForm.UpdatePopup();
        }

        private int GetItemIndexAt(int X, int  Y)
        {
            int Result = -1;
            if (ScrollBarVisible())
                Result = FScrollBar.Position + Y;
            else
                Result = Y;

            Result = Result / DROPDOWNITEMHEIGHT;

            if (Result > FItems.Count - 1)
                Result = FItems.Count - 1;

            return Result;
        }

        private void DoItemsChange(Object Sender)
        {
            if (FItems.Count < DROPDOWNCOUNT)
                FPopupForm.Height = DROPDOWNITEMHEIGHT * FItems.Count;
            else
                FPopupForm.Height = DROPDOWNITEMHEIGHT * DROPDOWNCOUNT;

            FScrollBar.Max = DROPDOWNITEMHEIGHT * FItems.Count;
            FScrollBar.Height = FPopupForm.Height;
        }

        private void GetDisplayRange(RECT AClientRect, ref int AStartIndex, ref int AEndIndex)
        {
            AStartIndex = 0;
            AEndIndex = FItems.Count - 1;
            if (ScrollBarVisible() && (FItems.Count > 0))
            {
                int vH = DROPDOWNITEMHEIGHT;
                for (int i = 0; i <= FItems.Count - 1; i++)
                {
                    if (vH - FScrollBar.Position > 0)
                    {
                        AStartIndex = i;
                        break;
                    }
                    else
                        vH = vH + DROPDOWNITEMHEIGHT;
                }

                for (int i = AStartIndex; i <= FItems.Count - 1; i++)
                {
                    if (vH - FScrollBar.Position > AClientRect.Bottom)
                    {
                        AEndIndex = i;
                        break;
                    }
                    else
                        vH = vH + DROPDOWNITEMHEIGHT;
                }
            }
        }

        private void DoPopupFormPaint(HCCanvas ACanvas, RECT AClientRect)
        {
            ACanvas.Brush.Color = HC.clInfoBk;
            ACanvas.FillRect(GetItemRect());  // AClientRect
            ACanvas.Font.Size = DROPDOWNFONTSIZE;  // 10号字，高14

            int vStartIndex = 0, vEndIndex = 0;
            GetDisplayRange(AClientRect, ref vStartIndex, ref vEndIndex);

            int vTop = 0;
            if (ScrollBarVisible())
                vTop = vStartIndex * DROPDOWNITEMHEIGHT - FScrollBar.Position;

            for (int i = vStartIndex; i <= vEndIndex; i++)
            {
                if (i == FMoveItemIndex)
                {
                    ACanvas.Brush.Color = HC.clHighlight;
                    ACanvas.FillRect(HC.Bounds(0, vTop, Width, DROPDOWNITEMHEIGHT));
                }
                else
                    ACanvas.Brush.Color = HC.clInfoBk;
                
                ACanvas.TextOut(2, vTop + 2, FItems[i]);
                vTop = vTop + DROPDOWNITEMHEIGHT;
            }

            if (ScrollBarVisible())
            {
                POINT vPt = new POINT();
                GDI.GetWindowOrgEx(ACanvas.Handle, ref vPt);
                GDI.SetWindowOrgEx(ACanvas.Handle, vPt.X - (Width - FScrollBar.Width), vPt.Y, ref vPt/*null*/);
                //User.MoveWindowOrg(ACanvas.Handle, Width - FScrollBar.Width, 0);
                GDI.IntersectClipRect(ACanvas.Handle, 0, 0, FScrollBar.Width, FScrollBar.Height);  // 创建新的剪切区域，该区域是当前剪切区域和一个特定矩形的交集
                FScrollBar.PaintToEx(ACanvas);
            }
        }

        private void DoPopupFormClose(Object Sender, EventArgs e)
        {
            if (FItemIndex < 0)
                Text = "";
            else
                Text = FItems[FItemIndex];

            FMoveItemIndex = -1;
            OwnerData.Style.UpdateInfoRePaint();
        }

        private void DoPopup()
        {
            if (FOnPopupItem != null)
                FOnPopupItem(this, null);

            POINT vPt = OwnerData.GetScreenCoord(FButtonDrawRect.Left - (this.Width - FButtonDrawRect.Width),
                FButtonDrawRect.Bottom + 1);

            //DoItemsChange(this);

            FPopupForm.Popup(vPt.X, vPt.Y);
        }

        private void DoPopupFormMouseDown(object sender, MouseEventArgs e)
        {
            RECT vRect = GetItemRect();
            if ((!HC.PtInRect(vRect, e.X, e.Y)) && ScrollBarVisible())
            {
                MouseEventArgs vEventArgs = new MouseEventArgs(e.Button, e.Clicks, e.X - vRect.Right, e.Y - vRect.Top, e.Delta);
                FScrollBar.DoMouseDown(vEventArgs);
            }
        }

        private void DoPopupFormMouseMove(object sender, MouseEventArgs e)
        {
            RECT vRect = GetItemRect();
            if (HC.PtInRect(vRect, e.X, e.Y))
            {
                int vIndex = GetItemIndexAt(e.X, e.Y);
                if (vIndex != FMoveItemIndex)
                {
                    FMoveItemIndex = vIndex;
                    FPopupForm.UpdatePopup();
                }
            }
            else
            if (ScrollBarVisible())
            {
                MouseEventArgs vEventArgs = new MouseEventArgs(e.Button, e.Clicks, e.X - vRect.Right, e.Y - vRect.Top, e.Delta);
                FScrollBar.DoMouseMove(vEventArgs);
            }
        }

        private void DoPopupFormMouseUp(object sender, MouseEventArgs e)
        {
            RECT vRect = GetItemRect();
            if (HC.PtInRect(vRect, e.X, e.Y))
            {
                FItemIndex = GetItemIndexAt(e.X, e.Y);
                FPopupForm.ClosePopup(false);
            }
            else
                if (ScrollBarVisible())
                {
                    MouseEventArgs vEventArgs = new MouseEventArgs(e.Button, e.Clicks, e.X - vRect.Right, e.Y - vRect.Top, e.Delta);
                    FScrollBar.DoMouseUp(vEventArgs);
                }
        }

        private void DoPopupFormMouseWheel(object sender, MouseEventArgs e)
        {
            if (ScrollBarVisible())
            {
                if (e.Delta > 0)
                    FScrollBar.Position = FScrollBar.Position - DROPDOWNITEMHEIGHT;
                else
                    FScrollBar.Position = FScrollBar.Position + DROPDOWNITEMHEIGHT;
            }
        }

        public override void FormatToDrawItem(HCCustomData ARichData, int AItemNo)
        {
            if (this.AutoSize)
            {
                ARichData.Style.ApplyTempStyle(TextStyleNo);
                SIZE vSize = new SIZE();
                if (this.Text != "")
                    vSize = ARichData.Style.TempCanvas.TextExtent(this.Text);
                else
                    vSize = ARichData.Style.TempCanvas.TextExtent("H");

                Width = FMargin + vSize.cx + FMargin + BTNWIDTH;  // 间距
                Height = FMargin + vSize.cy + FMargin;
            }
            if (Width < FMinWidth)
                Width = FMinWidth;
            if (Height < FMinHeight)
                Height = FMinHeight;
            
            FButtonRect = HC.Bounds(Width - BTNMARGIN - BTNWIDTH, BTNMARGIN, BTNWIDTH, Height - BTNMARGIN - BTNMARGIN);
            FPopupForm.Width = this.Width;
        }

        protected override void DoPaint(HCStyle AStyle, RECT ADrawRect, int ADataDrawTop, int ADataDrawBottom, 
            int ADataScreenTop, int ADataScreenBottom, HCCanvas ACanvas, PaintInfo APaintInfo)
        {
            base.DoPaint(AStyle, ADrawRect, ADataDrawTop, ADataDrawBottom, ADataScreenTop,
                ADataScreenBottom, ACanvas, APaintInfo);
    
            FButtonDrawRect = FButtonRect;
            FButtonDrawRect.Offset(ADrawRect.Left, ADrawRect.Top);

            if (IsSelectComplate)
                ACanvas.Brush.Color = AStyle.SelColor;
            else
            if (FMouseInButton)
                ACanvas.Brush.Color = HC.clMenu;
            else
                ACanvas.Brush.Color = HC.clWindow;

            ACanvas.FillRect(FButtonDrawRect);

            ACanvas.Pen.Color = Color.Black;
            int vLeft = FButtonDrawRect.Left + (BTNWIDTH - 7) / 2;
            int vTop = FButtonDrawRect.Top + (FButtonDrawRect.Height - 4) / 2;

            for (int i = 0; i <= 3; i++)
            {
                ACanvas.MoveTo(vLeft, vTop);
                ACanvas.LineTo(vLeft + 7 - i - i, vTop);
                vLeft++;
                vTop++;
            }
        }

        public override bool MouseDown(MouseEventArgs e)
        {
            if ((e.Button == MouseButtons.Left) && HC.PtInRect(FButtonRect, e.X, e.Y))
            {
                DoPopup();
                return true;
            }
            else
                return base.MouseDown(e);
        }

        public override bool MouseMove(MouseEventArgs e)
        {
            //if (FPopupForm.Open)
            //    HC.GCursor = Cursors.Default;

            if (HC.PtInRect(FButtonRect, e.X, e.Y))
            {
                if (!FMouseInButton)
                {
                    FMouseInButton = true;
                    OwnerData.Style.UpdateInfoRePaint();
                }
                
                HC.GCursor = Cursors.Default;
                return true;
            }
            else
            {
                if (FMouseInButton)
                {
                    FMouseInButton = false;
                    OwnerData.Style.UpdateInfoRePaint();
                }
                
                return base.MouseMove(e);
            }
        }

        public override void MouseLeave()
        {
            base.MouseLeave();
            FMouseInButton = false;
        }

        public override void GetCaretInfo(ref HCCaretInfo ACaretInfo)
        {
            base.GetCaretInfo(ref ACaretInfo);
            if ((!this.AutoSize) && (ACaretInfo.X > Width - BTNWIDTH))
                ACaretInfo.Visible = false;
        }

        protected void SetItems(List<string> Value)
        {
            if (!ReadOnly)
            {
                FItems.Clear();
                for (int i = 0; i < Value.Count; i++)
                    FItems.Add(Value[i]);
            }
        }

        protected void SetItemIndex(int Value)
        {
            if (!ReadOnly)
            {
                if ((Value >= 0) && (Value <= FItems.Count - 1))
                {
                    FItemIndex = Value;
                    Text = FItems[FItemIndex];
                }
                else
                {
                    FItemIndex = -1;
                    Text = "";
                }
            }
        }

        public byte DROPDOWNFONTSIZE = 10;  // 8号字
        public byte DROPDOWNITEMHEIGHT = 16;  // 8号字高度14 上下各加1间距
        public byte DROPDOWNCOUNT = 8;  // 下拉弹出时显示的Item数量

        public HCComboboxItem(HCCustomData AOwnerData, string AText) : base(AOwnerData, AText)
        {
            this.StyleNo = HCStyle.Combobox;
            Width = 80;
            FSaveItem = true;
            FItems = new List<string>();

            FScrollBar = new HCComScrollBar();
            FScrollBar.Orientation = Orientation.oriVertical;
            FScrollBar.OnScroll = DoScroll;

            FPopupForm = new HCPopupForm();
            FPopupForm.OnPaint = DoPopupFormPaint;
            FPopupForm.OnPopupClose = DoPopupFormClose;
            FPopupForm.OnMouseDown = DoPopupFormMouseDown;
            FPopupForm.OnMouseMove = DoPopupFormMouseMove;
            FPopupForm.OnMouseUp = DoPopupFormMouseUp;
            FPopupForm.OnMouseWheel = DoPopupFormMouseWheel; 
        }

        ~HCComboboxItem()
        {
            FPopupForm.Dispose();
            FScrollBar.Dispose();
        }

        public override void Assign(HCCustomItem Source)
        {
            base.Assign(Source);
            FItems.Clear();
            HCComboboxItem vCombobox = Source as HCComboboxItem;
            for (int i = 0; i < vCombobox.Items.Count; i++)
                FItems.Add(vCombobox.Items[i]);
        }

        public override void SaveToStream(Stream aStream, int aStart, int aEnd)
        {
            base.SaveToStream(aStream, aStart, aEnd);

            ushort vSize = 0;

            if (FSaveItem)
            {
                string vText = "";

                if (FItems.Count > 0)
                {
                    vText = FItems[0];
                    for (int i = 1; i < FItems.Count; i++)
                        vText = vText + HC.sLineBreak + FItems[i];
                }

                HC.HCSaveTextToStream(aStream, vText);
            }
            else
            {
                byte[] vBuffer = BitConverter.GetBytes(vSize);
                aStream.Write(vBuffer, 0, vBuffer.Length);
            }
        }

        public override void LoadFromStream(Stream AStream, HCStyle AStyle, ushort aFileVersion)
        {
            base.LoadFromStream(AStream, AStyle, aFileVersion);
            FItems.Clear();
            string vText = "";
            HC.HCLoadTextFromStream(AStream, ref vText, aFileVersion);
            string[] vStrings = vText.Split(new string[] { HC.sLineBreak }, StringSplitOptions.None);
            for (int i = 0; i < vStrings.Length; i++)
                FItems.Add(vStrings[i]);
        }

        public override void ToXml(XmlElement aNode)
        {
            base.ToXml(aNode);
            if (FSaveItem)
            {
                string vText = "";

                if (FItems.Count > 0)
                {
                    vText = FItems[0];
                    for (int i = 1; i < FItems.Count - 1; i++)
                        vText = vText + HC.sLineBreak + FItems[i];
                }

                aNode.SetAttribute("item", vText);
            }
        }

        public override void ParseXml(XmlElement aNode)
        {
            base.ParseXml(aNode);
            FItems.Clear();
            string vText = aNode.Attributes["item"].Value;
            string[] vStrings = vText.Split(new string[] { HC.sLineBreak }, StringSplitOptions.None);

            for (int i = 0; i < vStrings.Length; i++)
                FItems.Add(vStrings[i]);
        }

        public List<string> Items
        {
            get { return FItems; }
            set { SetItems(value); }
        }

        public int ItemIndex
        {
            get { return FItemIndex; }
            set { SetItemIndex(value); }
        }

        public bool SaveItem
        {
            get { return FSaveItem; }
            set { FSaveItem = value; }
        }
        public EventHandler OnPopupItem
        {
            get { return FOnPopupItem; }
            set { FOnPopupItem = value; }
        }
    }
}
