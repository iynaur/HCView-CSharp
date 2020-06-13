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
using HC.Win32;
using HC.View;
using System.IO;
using System.Xml;

namespace EMRView
{
    public static class GroupProp : Object
    {
        /// <summary> 数据组唯一索引 </summary>
        public const string Index = "Index";
        /// <summary> 数据组名称 </summary>
        public const string Name = "Name";
        /// <summary> 数据组类型 </summary>
        public const string SubType = "RT";
        /// <summary> 全部属性 </summary>
        public const string Propertys = "Propertys";
    }

    public static class SubType : Object
    {
        /// <summary> 病程 </summary>
        public const string Proc = "P";
    }

    public class DeGroup : HCDomainItem
    {
        private bool FReadOnly;
        #if PROCSERIES
        private bool FIsProc;
        #endif
        private Dictionary<string, string> FPropertys;

        private string GetValue(string key)
        {
            if (FPropertys.Keys.Contains(key))
                return FPropertys[key];
            else
                return "";
        }

        private void SetValue(string key, string value)
        {
            if (value.IndexOf("=") >= 0)
                throw new Exception("属性值中不允许有=号");

            FPropertys[key] = value;
        }

        #if PROCSERIES
        private bool GetIsProcBegin()
        {
            if (this.MarkType == MarkType.cmtBeg)
                return FIsProc;
            else
                return false;
        }

        private bool GetIsProcEnd()
        {
            if (this.MarkType == MarkType.cmtEnd)
                return FIsProc;
            else
                return false;
        }
        #endif

        protected override void DoPaint(HCStyle aStyle, RECT aDrawRect, int aDataDrawTop, int aDataDrawBottom, 
            int aDataScreenTop, int aDataScreenBottom, HCCanvas aCanvas, PaintInfo aPaintInfo)
        {
            base.DoPaint(aStyle, aDrawRect, aDataDrawTop, aDataDrawBottom, aDataScreenTop, aDataScreenBottom, aCanvas, aPaintInfo);
        }

        public DeGroup(HCCustomData aOwnerData) : base(aOwnerData)
        {
            FPropertys = new Dictionary<string, string>();
            FReadOnly = false;
            #if PROCSERIES
            FIsProc = false;
            #endif
        }

        ~DeGroup()
        {

        }

        public override void SaveToStream(Stream aStream, int aStart, int aEnd)
        {
            base.SaveToStream(aStream, aStart, aEnd);
            HC.View.HC.HCSaveTextToStream(aStream, DeProp.GetPropertyString(FPropertys));
        }

        public override void LoadFromStream(Stream aStream, HCStyle aStyle, ushort aFileVersion)
        {
            base.LoadFromStream(aStream, aStyle, aFileVersion);
            string vS = "";
            HC.View.HC.HCLoadTextFromStream(aStream, ref vS, aFileVersion);
            DeProp.SetPropertyString(vS, FPropertys);
            CheckPropertys();
        }

        public override void Assign(HCCustomItem source)
        {
            base.Assign(source);
            string vS = DeProp.GetPropertyString((source as DeGroup).Propertys);
            FReadOnly = (source as DeGroup).ReadOnly;
            DeProp.SetPropertyString(vS, FPropertys);
            CheckPropertys();
        }

        public override int GetOffsetAt(int x)
        {
            #if PROCSERIES
            if (GetIsProcEnd())
                return HC.View.HC.OffsetBefor;
            else
            if (GetIsProcBegin())
                return HC.View.HC.OffsetAfter;
            else
            #endif
                return base.GetOffsetAt(x);
        }

        public override void ToXml(XmlElement aNode)
        {
            base.ToXml(aNode);
            aNode.SetAttribute("property", DeProp.GetPropertyString(FPropertys));
        }

        public override void ParseXml(XmlElement aNode)
        {
            base.ParseXml(aNode);
            DeProp.SetPropertyString(aNode.Attributes["property"].Value, FPropertys);
            CheckPropertys();
        }

        public void ToJson(string aJsonObj)
        {

        }

        public void ParseJson(string aJsonObj)
        {

        }

        public void CheckPropertys()
        {
            #if PROCSERIES
            FIsProc = this.GetValue(GroupProp.SubType) == SubType.Proc;
            #endif
        }

        public Dictionary<string, string> Propertys
        {
            get { return FPropertys; }
        }

        public bool ReadOnly
        {
            get { return FReadOnly; }
            set { FReadOnly = value; }
        }

        #if PROCSERIES
        public bool IsProc
        {
            get { return FIsProc; }
        }

        public bool IsProcBegin
        {
            get { return GetIsProcBegin(); }
        }

        public bool IsProcEnd
        {
            get { return GetIsProcEnd(); }
        }
        #endif

        public string this[string aKey]
        {
            get { return GetValue(aKey); }
            set { SetValue(aKey, value); }
        }
    }

    public class ProcInfo : HCDomainInfo
    {
        private string FIndex;
  
        public ProcInfo() : base()
        {
            FIndex = "";
        }

        public override void Clear()
        {
            FIndex = "";
            base.Clear();
        }

        public string Index
        {
            get { return FIndex; }
            set { FIndex = value; }
        }
    }
}
