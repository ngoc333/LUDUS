using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Xml.Linq;

namespace LUDUS.Utils {
    public class RegionInfo {
        public string Name { get; set; }
        public Rectangle Rect { get; set; }
        public string Group { get; set; }
    }

    public static class RegionLoader {
        public static List<RegionInfo> LoadPresetRegions(string xmlPath) {
            var list = new List<RegionInfo>();
            if (string.IsNullOrEmpty(xmlPath) || !File.Exists(xmlPath))
                return list;

            var doc = XDocument.Load(xmlPath);
            var regions = doc.Root;
            if (regions == null) return list;

            // Đọc ScreenRegions
            var screenRegions = regions.Element("ScreenRegions");
            if (screenRegions != null) {
                foreach (var xe in screenRegions.Elements("Region")) {
                    AddRegionToList(list, xe, "ScreenRegions");
                }
            }

            // Đọc MySideCell
            var mySideCell = regions.Element("MySideCell");
            if (mySideCell != null) {
                foreach (var xe in mySideCell.Elements("Region")) {
                    AddRegionToList(list, xe, "MySideCell");
                }
            }

            // Đọc HeroInfo
            var heroInfo = regions.Element("HeroInfo");
            if (mySideCell != null) {
                foreach (var xe in heroInfo.Elements("Region")) {
                    AddRegionToList(list, xe, "HeroInfo");
                }
            }

            return list;
        }

        private static void AddRegionToList(List<RegionInfo> list, XElement xe, string group) {
            var nameAttr = xe.Attribute("Name");
            var xAttr = xe.Attribute("X");
            var yAttr = xe.Attribute("Y");
            var wAttr = xe.Attribute("Width");
            var hAttr = xe.Attribute("Height");
            if (nameAttr == null || xAttr == null || yAttr == null || wAttr == null || hAttr == null)
                return;

            list.Add(new RegionInfo {
                Name = nameAttr.Value,
                Rect = new Rectangle(
                    int.Parse(xAttr.Value),
                    int.Parse(yAttr.Value),
                    int.Parse(wAttr.Value),
                    int.Parse(hAttr.Value)
                ),
                Group = group
            });
        }
    }
}
