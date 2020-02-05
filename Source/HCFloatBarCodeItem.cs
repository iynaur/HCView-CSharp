﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using HC.Win32;

namespace HC.View
{
    public class HCFloatBarCodeItem : HCCustomFloatItem
    {
        private bool FAutoSize, FShowText;
        private Byte FPenWidth;
        private string FText;

        protected override string GetText()
        {
            return FText;
        }

        protected override void SetText(string value)
        {
            if (FText != value)
            {
                FText = value;
            }
        }

        public HCFloatBarCodeItem(HCCustomData aOwnerData) : base(aOwnerData)
        {
            StyleNo = HCStyle.FloatBarCode;
            FAutoSize = true;
            FShowText = true;
            FPenWidth = 2;
            Width = 80;
            Height = 60;
            SetText("0000");
        }

        public override void Assign(HCCustomItem source)
        {
            base.Assign(source);
            FText = (source as HCFloatBarCodeItem).Text;
        }

        protected override void DoPaint(HCStyle aStyle, RECT aDrawRect, int aDataDrawTop, int aDataDrawBottom, int aDataScreenTop, int aDataScreenBottom, HCCanvas aCanvas, PaintInfo aPaintInfo)
        {
            using (Image vBitmap = SharpZXingBarCode.Create(FText, 3, Width, Height))
            {
                if (vBitmap != null)
                {
                    if (aPaintInfo.Print)
                    {
                        aCanvas.StretchPrintDrawImage(aDrawRect, vBitmap);
                    }
                    else
                        aCanvas.StretchDraw(aDrawRect, vBitmap);
                }
            }
            // 绘制一维码
            base.DoPaint(aStyle, aDrawRect, aDataDrawTop, aDataDrawBottom, aDataScreenTop, aDataScreenBottom,
                aCanvas, aPaintInfo);
        }

        public override void SaveToStream(Stream aStream, int aStart, int aEnd)
        {
            base.SaveToStream(aStream, aStart, aEnd);
            HC.HCSaveTextToStream(aStream, FText);
            byte[] vBuffer = BitConverter.GetBytes(FAutoSize);
            aStream.Write(vBuffer, 0, vBuffer.Length);

            vBuffer = BitConverter.GetBytes(FShowText);
            aStream.Write(vBuffer, 0, vBuffer.Length);

            aStream.WriteByte(FPenWidth);
        }

        public override void LoadFromStream(Stream aStream, HCStyle aStyle, ushort aFileVersion)
        {
            base.LoadFromStream(aStream, aStyle, aFileVersion);
            HC.HCLoadTextFromStream(aStream, ref FText, aFileVersion);
            if (aFileVersion > 34)
            {
                byte[] vBuffer = BitConverter.GetBytes(FAutoSize);
                aStream.Read(vBuffer, 0, vBuffer.Length);
                FAutoSize = BitConverter.ToBoolean(vBuffer, 0);

                vBuffer = BitConverter.GetBytes(FShowText);
                aStream.Read(vBuffer, 0, vBuffer.Length);
                FShowText = BitConverter.ToBoolean(vBuffer, 0);

                FPenWidth = (Byte)aStream.ReadByte();
            }
        }

        public override void ToXml(System.Xml.XmlElement aNode)
        {
            base.ToXml(aNode);
            aNode.InnerText = FText;

            if (FAutoSize)
                aNode.SetAttribute("autosize", "1");
            else
                aNode.SetAttribute("autosize", "0");

            if (FShowText)
                aNode.SetAttribute("showtext", "1");
            else
                aNode.SetAttribute("showtext", "0");

            aNode.SetAttribute("penwidth", FPenWidth.ToString());
        }

        public override void ParseXml(System.Xml.XmlElement aNode)
        {
            base.ParseXml(aNode);
            FText = aNode.InnerText;

            if (aNode.HasAttribute("autosize"))
                FAutoSize = bool.Parse(aNode.Attributes["autosize"].Value);
            else
                FAutoSize = true;

            if (aNode.HasAttribute("showtext"))
                FShowText = bool.Parse(aNode.Attributes["showtext"].Value);
            else
                FShowText = true;

            if (aNode.HasAttribute("penwidth"))
                FPenWidth = Byte.Parse(aNode.Attributes["penwidth"].Value);
            else
                FPenWidth = 2;
        }

        public Byte PenWidth
        {
            get { return FPenWidth; }
            set { FPenWidth = value; }
        }

        public bool AutoSize
        {
            get { return FAutoSize; }
            set { FAutoSize = value; }
        }

        public bool ShowText
        {
            get { return FShowText; }
            set { FShowText = value; }
        }
    }
}
