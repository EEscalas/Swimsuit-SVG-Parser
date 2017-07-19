using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Xml;
using System.Xml.Linq;

namespace ParsingTests
{
    class Program
    {
        static void ParseSVG(string location)
        {
            //create XDocument
            XDocument drawing = XDocument.Load(location);

            GetCreator(drawing);

            // create color Index that links str and fil to hex/color values
            Dictionary<string, string> colorIndex = new Dictionary<string, string>();
            colorIndex = findColors(drawing);

            // get the style ID
            string styID = GetStyleID(drawing);

            // retrieve colors from design
            DesignLayer _designLayer = new DesignLayer();
            _designLayer = ParsePrint("design", drawing, colorIndex);

            // retrieve colors from other layers
            List<Layer> lay = new List<Layer>();
            CreateLayers("design", drawing, lay);
            ParseLayers(drawing, lay, colorIndex);

            // print results
            PrintParsedSVG(styID, _designLayer, lay);
        }
        static Dictionary<string, string> findColors(XDocument xdoc)
        {
            XElement svg_Element = xdoc.Root;
            IEnumerable<XElement> test = from e1 in svg_Element.Elements("{http://www.w3.org/2000/svg}defs")
                                         select e1;

            Dictionary<string, string> index = new Dictionary<string, string>();

            foreach (XElement ee in test)
            {
                string cData = ee.Value;
                string[] words = cData.Split('\n');
                foreach (string line in words)
                {
                    string key = "";
                    foreach (char c in line)
                    {
                        if (c == ' ' || c == '.')
                            continue;
                        if (c == '{')
                            break;
                        key += c;
                    }
                    bool value = false;
                    string hex = "";
                    foreach (char c in line)
                    {
                        if (c == ':') { value = true; continue; }
                        if (!value) continue;
                        if (c == ';' || c == '}') break;
                        hex += c;
                    }

                    if (key != "" && hex != "")
                        index.Add(key, hex);
                }
            }
            return index;
        }
        static void PrintParsedSVG(string styleID, DesignLayer mainDesign, List<Layer> otherLayers)
        {
            Console.WriteLine("Style ID: " + styleID);
            Console.WriteLine("Main Design Layer Colors: ");
            mainDesign.printCol();
            if (mainDesign.isSwatch)
            {
                Console.WriteLine("Swatch Color Numbers: ");
                foreach (string colo in mainDesign.swatches)
                {
                    Console.WriteLine("        " + colo);
                }
                Console.WriteLine();
            }
            foreach (Layer m in otherLayers)
            {
                Console.WriteLine("Layer Name: " + m.name);
                Console.WriteLine("Layer Color: ");
                m.printCol();
            }
        }
        static void CreateLayers(string layerName, XDocument xDoc, List<Layer> layers)
        {
            XElement svg_Element = xDoc.Root;

            IEnumerable<XElement> test = from e1 in svg_Element.Elements("{http://www.w3.org/2000/svg}g")
                                         select e1;

            foreach (XElement ee in test)
            {
                if (ee.Attribute("id").Value == "style" || ee.Attribute("id").Value == layerName)
                    continue;

                Layer temp = new Layer();
                temp.name = (ee.Attribute("id").Value);

                layers.Add(temp);
            }
        }
        static void ParseLayers(XDocument xDoc, List<Layer> layers, Dictionary<string, string> index)
        {
            XElement svg_Element = xDoc.Root;
            foreach (Layer element in layers)
            {
                string workingWith = element.name;
                IEnumerable<XElement> test = from e1 in svg_Element.Elements("{http://www.w3.org/2000/svg}g")
                                             select e1;

                foreach (XElement ee in test)
                {
                    if (ee.Attribute("id").Value != workingWith)
                        continue;

                    IEnumerable<XElement> test2 = from e2 in ee.Elements("{http://www.w3.org/2000/svg}g")
                                                  select e2;

                    foreach (XElement ee2 in test2)
                    {
                        // extract colors in paths

                        IEnumerable<XElement> test3 = from ee3 in ee2.Elements("{http://www.w3.org/2000/svg}path")
                                                      select ee3;
                        foreach (XElement epath in test3)
                        {
                            string c = epath.Attribute("class").Value.ToString();
                            AddColors(xDoc, c, index, element);
                        }
                        
                        // extract colors in lines

                        IEnumerable<XElement> test4 = from ee3 in ee2.Elements("{http://www.w3.org/2000/svg}line")
                                                      select ee3;

                        foreach (XElement epath in test4)
                        {
                            string c = epath.Attribute("class").Value.ToString();
                            AddColors(xDoc, c, index, element);
                        }
                    }
                }
            }
        }
        static DesignLayer ParsePrint(string layerName, XDocument xdoc, Dictionary<string, string> index) // lay is the input for the name of the design layer
        {
            XElement svg_Element = xdoc.Root;

            // create new layer to represent the suit design
            DesignLayer design = new DesignLayer();
            design.name = "Design";

            IEnumerable<XElement> test = from e1 in svg_Element.Elements("{http://www.w3.org/2000/svg}g")
                                         select e1;

            // find the design node
            foreach (XElement ee in test)
            {
                if (ee.Attribute("id").Value != layerName)
                    continue;

                //extract color information from every descendant node of the design XElement
                foreach (XElement xe in ee.Descendants())
                {
                    string nodeName = xe.Name.ToString();
                    string colors = "none";
                    if (nodeName == "{http://www.w3.org/2000/svg}g")
                    {
                        foreach (XElement shape in xe.Descendants())    // check the descendants inside the extra g node
                        {
                            if (nodeName == "{http://www.w3.org/2000/svg}metadata") continue;
                            if (nodeName == "{http://www.w3.org/2000/svg}text") // need to add to non-descendent version too
                            {
                                string shapeVal1 = xe.Value;
                                string swatchVal1 = "Swatch";
                                if (shapeVal1.Contains(swatchVal1))
                                {
                                    design.isSwatch = true;
                                    string swatchPMS1 = "";
                                    bool PMS = false;
                                    foreach (char c in shapeVal1)
                                    {
                                        if (c == ':') PMS = true;
                                        if (c == ' ') continue;
                                        if (PMS && char.IsDigit(c)) swatchPMS1 += c;
                                        if (swatchPMS1.Length == 4) PMS = false;
                                    }
                                    swatchPMS1 = swatchPMS1.ToString();
                                    design.addSwatch(swatchPMS1);
                                }
                                continue;
                            }
                            string checkIfHasClass1 = (string)xe.Attribute("class");
                            string checkIfHasFill1 = (string)xe.Attribute("fill");
                            string checkIfHasStr1 = (string)xe.Attribute("stroke");
                            if (!String.IsNullOrEmpty(checkIfHasClass1))   // check to see if there is a class attribute
                            {
                                colors = xe.Attribute("class").Value.ToString();
                                AddColors(xdoc, colors, index, design);
                            }
                            else
                            {
                                if (!String.IsNullOrEmpty(checkIfHasFill1)) // if not, the colors are located in fil and str
                                {
                                    colors = xe.Attribute("fill").Value.ToString();
                                    AddColors(xdoc, colors, index, design);
                                }
                                if (!String.IsNullOrEmpty(checkIfHasStr1))
                                {
                                    colors = xe.Attribute("stroke").Value.ToString();
                                    AddColors(xdoc, colors, index, design);
                                }
                            }
                        }
                    }
                    else if (nodeName == "{http://www.w3.org/2000/svg}metadata") continue;
                    if (nodeName == "{http://www.w3.org/2000/svg}text") // need to add to non-descendent version too
                    {
                        string shapeVal = xe.Value;
                        string swatchVal = "Swatch";
                        if (shapeVal.Contains(swatchVal))
                        {
                            design.isSwatch = true;
                            string swatchPMS = "";
                            bool PMS = false;
                            foreach (char c in shapeVal)
                            {
                                if (c == ':') PMS = true;
                                if (c == ' ') continue;
                                if (PMS && char.IsDigit(c)) swatchPMS += c;
                                if (swatchPMS.Length == 4) PMS = false;
                            }
                            swatchPMS = swatchPMS.ToString();
                            design.addSwatch(swatchPMS);
                        }
                        continue;
                    }
                    string checkIfHasClass = (string)xe.Attribute("class");
                    string checkIfHasFill = (string)xe.Attribute("fill");
                    string checkIfHasStr = (string)xe.Attribute("stroke");
                    if (!String.IsNullOrEmpty(checkIfHasClass))   // check to see if there is a class attribute
                    {
                        colors = xe.Attribute("class").Value.ToString();
                        AddColors(xdoc, colors, index, design);
                    }
                    else
                    {
                        if (!String.IsNullOrEmpty(checkIfHasFill)) // if not, the colors are located in fil and str
                        {
                            colors = xe.Attribute("fill").Value.ToString();
                            AddColors(xdoc, colors, index, design);
                        }
                        if (!String.IsNullOrEmpty(checkIfHasStr))
                        {
                            colors = xe.Attribute("stroke").Value.ToString();
                            AddColors(xdoc, colors, index, design);
                        }
                    }
                }
            }
            return design;
        }
        static void AddColors(XDocument xdoc, string colorInput, Dictionary<string, string> index, Layer design)
        {
            string[] words = colorInput.Split(' ');
            foreach (string word in words)
            {
                string value = index[word];
                if (index[word] == "none")
                    continue;
                if (value[0] == 'u')
                    AddGradientColors(xdoc, design);
                else if (word[0] == 'f')
                    design.addColF(value);
                else if (word[0] == 's')
                    design.addColS(value);
            }
        }
        static void AddGradientColors(XDocument doc, Layer design)
        {
            XElement svg_Element = doc.Root;
            XElement defs = svg_Element.Element("{http://www.w3.org/2000/svg}defs");
            XElement linGrad = defs.Element("{http://www.w3.org/2000/svg}linearGradient");
            string colors;
            foreach (XElement elem in linGrad.Descendants())
            {
                colors = elem.Attribute("style").ToString();
                string[] words = colors.Split('\n');
                foreach (string line in words)
                {
                    string key = "";
                    bool isHex = false;
                    bool passedSemiColon = false;
                    foreach (char c in line)
                    {
                        if (c == ';') passedSemiColon = true;
                        if (passedSemiColon)
                        {
                            if (c == ':')
                            {
                                isHex = true;
                                continue;
                            }
                        }
                        if (isHex && passedSemiColon)
                        {
                            if (c == '"') break;
                            key += c;
                        }
                    }
                    design.addColF(key);
                }
            }
            design.addColF("processed colors");
        }
        static string GetStyleID(XDocument xdoc)
        {
            XElement svg_Element = xdoc.Root;

            IEnumerable<XElement> test = from e1 in svg_Element.Elements("{http://www.w3.org/2000/svg}g")
                                         select e1;

            string ID = "ERROR";

            foreach (XElement style in test)
            {
                if (style.Attribute("id").Value == "style")
                {
                    ID = style.Value;
                }
            }

            ID = clean(ID);
            return ID;
        }
        static string clean(string str)
        {
            str = str.ToLower();
            string[] words = str.Split(' ');
            string key = "";
            bool isWord = false;
            foreach (string word in words)
            {
                isWord = false;
                foreach (char c in word)
                {
                    if (Char.IsLetterOrDigit(c) || c == '.')
                    {
                        key += c;
                        isWord = true;
                    }
                }
                if (isWord)
                    key += '-';
            }
            key = key.Trim('-');
            return key;
        }
        static bool isValidHexCode(string hexCode)
        {
            if (hexCode[0] != '#')
            {
                Console.WriteLine("This is an invalid color input format. Your input must start with a '#'.");
                return false;
            }
            else if (hexCode.Length != 7)
            {
                Console.WriteLine("This is an invalid color input format. Your input must be 7 characters long");
                return false;
            }
            foreach (char c in hexCode)
            {
                if (c == '#') continue;
                else if (Char.IsDigit(c)) continue;
                else if (c == 'A' || c == 'B' || c == 'C' || c == 'D' || c == 'E' || c == 'F') continue;
                else if (c == 'a' || c == 'b' || c == 'c' || c == 'd' || c == 'e' || c == 'f')
                {
                    Console.WriteLine("This is an invalid color input format. Make sure all hex values contain only capital letters.");
                    return false;
                }
                
                else
                {
                    Console.WriteLine("This is an invalid color input format. Your input must be in hex value format.");
                    return false;
                }
            }
            return true;
        }
        static XDocument ChangeColor (XDocument drawing, string from, string to)
        {
            
            //extract CDATA to then later re-insert it into the xml file
            XElement svg_Element = drawing.Root;
            IEnumerable<XElement> test = from e1 in svg_Element.Elements("{http://www.w3.org/2000/svg}defs")
                                         select e1;
            foreach (XElement defs in test)
            {
                IEnumerable<XElement> test2 = from e1 in defs.Elements("{http://www.w3.org/2000/svg}style")
                                             select e1;

                foreach (XElement data in test2)
                {
                    string cdata = data.Value;
                    cdata = cdata.Replace(from, to);
                    data.Value = cdata;
                }

            }          
    
            return drawing;
        }
        static string GetCreator(XDocument drawing)
        {
            string creator = "";
            bool done = false;
            bool wordDone = false;
            string svg = drawing.ToString();
            int counter = 0;
            while (!done)
            {
                if (svg[counter] == '<' && svg[counter + 1] == '!' && svg[counter + 2] == '-')
                {
                    counter += 14;
                    while (!wordDone)
                    {
                        if (svg[counter + 1] == '-' && svg[counter + 2] == '-') wordDone = true;
                        creator += svg[counter];
                        counter++;
                    }
                    done = true;
                }
                counter++;
            }
            Console.WriteLine(creator);
            return creator;
        }

        static void Main(string[] args)
        {
            string fileLocation = "data\\drawing.xml";
            Console.Write("You are using: ");
            ParseSVG(fileLocation);

            Console.WriteLine("Would you like to change a color? (Y/N)");
            string changeColor = Console.ReadLine();
            while (changeColor != "Y" && changeColor != "N")
            {
                Console.WriteLine("Invalid Input. Try again (Y/N):");
                changeColor = Console.ReadLine();
            }
            if (changeColor == "Y")
            {
                Console.WriteLine("What color would you like to change? (#_ _ _ _ _ _)");
                string colorToChange = Console.ReadLine();
                while (!isValidHexCode(colorToChange))
                {
                    Console.WriteLine("Please try again:");
                    colorToChange = Console.ReadLine();
                }
                Console.WriteLine("What color would you like to change it to? (#_ _ _ _ _ _)");
                string newColor = Console.ReadLine();
                while (!isValidHexCode(newColor))
                {
                    Console.WriteLine("Please try again:");
                    newColor = Console.ReadLine();
                }
                XDocument newSVG = XDocument.Load(fileLocation);
                ChangeColor(newSVG, colorToChange, newColor);
                //newSVG.Save("data\\modifiedColors.xml");
                newSVG.Save("changedColors.xml");
                //Console.WriteLine(File.ReadAllText("changedColors.xml"));
                
            }
        }
        private class Layer
        {
            public void addColF(string color)
            {
                fil.Add(color);

            }
            public void addColS(string color)
            {
                str.Add(color);
            }
            public void printCol()
            {
                foreach (string purdy in fil)
                {
                    Console.WriteLine("        " + purdy);
                }
                foreach (string purdys in str)
                {
                    Console.WriteLine("        " + purdys);
                }
                Console.WriteLine();
            }

            public string name { get; set; }
            private HashSet<string> fil = new HashSet<string>();
            private HashSet<string> str = new HashSet<string>();
        }
        private class DesignLayer :Layer
        {
            public void addSwatch(string swatch)
            {
                swatches.Add(swatch);
            }
            public bool isSwatch { get; set; }
            public List<string> swatches = new List<string>();
        }
    }
}
