using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Win32;



/// <summary>
/// This is the C# script of the Multiterm to TBX converter. It accepts a JSON mapping file and an XML TBX file and returns a properly formatted TBX file as its output.
/// 
/// Currently only the JSON file parsing methods are in place.
/// 
/// Known limitations:
/// 
/// MINOR
/// 
/// ** The teasp class does not account for the possibility of an object for the substitution value
/// ** Queue-draining does not account for the lack of any of the 3 possible keys: conceptGrp, languageGrp, and termGrp
/// 
/// CRITICAL
/// 
/// ** The conversion of the one-level mapping from a JObject to a dictionary is assumed to maintain all subsequent data, but is not tested. 
/// ** template-set parsing does not account for plain [[[teasp]]] elements, it is unknown how the parser will react. (Probably poorly)
/// 
/// 
/// </summary>



namespace Csh_mt2tbx
{

    public class extendedTeaspStorageManager
    {
        public List<string[]> valueGroupCollection; // Each string[] will correspond with the teasp in the same position
        public List<teaspWithSub> correspondingValGrpTeasps; // Every one of these will have substitutions, and therefore the default will not be in this list
        public teaspNoSub defaultTeaspSub;

        public extendedTeaspStorageManager(List<string[]> ls, List<teaspWithSub> lt, teaspNoSub dt)
        {
            valueGroupCollection = ls;
            correspondingValGrpTeasps = lt;
            defaultTeaspSub = dt;
        }

        public List<string[]> getValueGroupCollection()
        {
            return valueGroupCollection;
        }

        public List<teaspWithSub> getCorrespondingValGrpTeasps()
        {
            return correspondingValGrpTeasps;
        }

        public teaspNoSub getDefaultTeaspSub()
        {
            return defaultTeaspSub;
        }
    }

    // This is the vanilla teasp template. It takes an array of strings as its constructor and stores the appropriate values, although it currently does not account for the possibility of a 
    // dictionary-like object for a substitution.

    public class teaspNoSub
    {
        public string target;
        public string elementOrAttributes;
        public string substitution;
        public string placement;

        public teaspNoSub(string t, string ea, string s, string p)
        {
            target = t;
            elementOrAttributes = ea;
            substitution = s;
            placement = p;
        }

        public string getTarget()
        {
            return target;
        }

        public string getElementOrAttribute()
        {
            return elementOrAttributes;
        }

        public string getSubstitution()
        {
            return substitution;
        }

        public string getPlacement()
        {
            return placement;
        }

    }

    public class teaspWithSub
    {
        public string target;
        public string elementOrAttributes;
        public Dictionary<string,string> substitution;
        public string placement;

        public teaspWithSub(string t, string ea, Dictionary<string,string> s, string p)
        {
            target = t;
            elementOrAttributes = ea;
            foreach(KeyValuePair<string,string> entry in s)
            {
                substitution.Add(entry.Key, entry.Value);
            }
            placement = p;
        }

        public string getTarget()
        {
            return target;
        }

        public string getElementOrAttribute()
        {
            return elementOrAttributes;
        }

        public Dictionary<string, string> getSubstitution()
        {
            return substitution;
        }

        public string getPlacement()
        {
            return placement;
        }

    }

    // The template set is constructed by seperating all of the template-set jObjects from the dictionaries, and stored in individual Lists. Each list is then parsed and seperated into 
    // the default teasp and value-groups, followed by the subsequent teasps.

    public class templateSet
    {
        public List<object> conceptMappingTemplates = new List<object> ();
        public List<string> conceptMappingTemplatesKeys = new List<string> ();
        public List<object> languageMappingTemplates = new List<object> ();
        public List<string> languageMappingTemplatesKeys = new List<string> ();
        public List<object> termMappingTemplates = new List<object> ();
        public List<string> termMappingTemplatesKeys = new List<string> ();
        public object[] castObjArray;
        public string key;
        public teaspNoSub teaspNS;
        public teaspWithSub teaspWS;
        public int handler = 0;
        public int cKeyCounter = 0;
        public int lKeyCounter = 0;
        public int tKeyCounter = 0;
        public string[] valGrp;
        public List<string[]> ls;
        public List<teaspWithSub> lt;
        public teaspNoSub dt;
        public extendedTeaspStorageManager ETSM;


        // // // //

        // This is the Dicitonary that will contain the Mapping Templates Strings and an object (Either a plain teasp or a extendedTeaspStorageManager object).
        // Regardless of what kind of object each Key-Value pair has, type will be determined at runtime and processing will be done then.

        public Dictionary<string, object> grandMasterDictionary = new Dictionary<string, object> ();

        // // // //


        public string t;
        public string ea;
        // Declare s at runtime
        public string p;


        public templateSet(Dictionary<string, object> c, Dictionary<string, object> l, Dictionary<string, object> t)
        {
            foreach(var entry in c)
            {
                object tempUNK1 = entry.Value;
                conceptMappingTemplates.Add(tempUNK1);

                key = entry.Key;
                conceptMappingTemplatesKeys.Add(key);
            }
            foreach(var entry2 in l)
            {
                object tempUNK2 = entry2.Value;
                languageMappingTemplates.Add(tempUNK2);

                key = entry2.Key;
                languageMappingTemplatesKeys.Add(key);
            }
            foreach (var entry3 in t)
            {
                object tempUNK3 = entry3.Value;
                termMappingTemplates.Add(tempUNK3);

                key = entry3.Key;
                termMappingTemplatesKeys.Add(key);
            }

            convertTemplateSets();
        }

        public Dictionary<string, object> getGrandMasterDictionary()
        {
            return grandMasterDictionary;
        }

        public void convertTemplateSets()
        {

            // Logic: A plain template-set will have only 1 internal array, where those that have value groups will have multiple internal arrays, and the first array will hold value groups

            foreach (object j in conceptMappingTemplates)
            {
                castObjArray = (object[])j;

                if (castObjArray.Length == 1) // This is "plain" template set
                {
                    object[] temp = (object[])castObjArray[0]; //  Grabs onto plain teaps array
                    object[] teasp = (object[])temp[0]; // We now have access to the info of the teasp! Cannot be a plain string[] because the substitution may still be an array.

                    t = (string)teasp[0];
                    ea = (string)teasp[1];
                    string s = (string)teasp[2]; // It is safe to assume it will be NULL, only Teasps that are sprung from value-groups with have subs and because length is only 1, we know there are none
                    p = (string)teasp[3];
        
                    teaspNS = new teaspNoSub(t, ea, s, p);
                    grandMasterDictionary.Add(conceptMappingTemplatesKeys[cKeyCounter], teaspNS);
                    cKeyCounter++;

                }
                else if (castObjArray.Length != 0 && castObjArray.Length > 1) // This is a template set with Value groups 
                {

                    object[] temp = (object[])castObjArray[0]; // This will have the default Teasp and subsequent value groups
                    object[] deftsp = (object[])temp[0]; // Grab the default teasp
                    t = (string)deftsp[0];
                    ea = (string)deftsp[1];
                    string s = (string)deftsp[2]; // As the default, we know it will always be NULL
                    p = (string)deftsp[3];

                    teaspNS = new teaspNoSub(t, ea, s, p); // This is now ready to give to the extendedTeaspStorageManager
                    dt = teaspNS;

                    temp = temp.Skip(1).ToArray(); // We dont want the first array, it is its own teasp, this array now just has value groups
                    foreach(string[] st in temp)
                    {
                        ls.Add(st); // This populates the list of string[] for the extendedTeaspStorageManager with all value-groups
                    }
                    
                    castObjArray = castObjArray.Skip(1).ToArray(); // We dont want the first array, because it will be handled seperately (above)
                    foreach (object[] tsp in castObjArray) // This handles the teasps that correspond with each value-group
                    {
                        t = (string)tsp[0];
                        ea = (string)tsp[1];
                        Dictionary<string, string> ds = (Dictionary<string, string>)tsp[2]; // If there were no substitution we wouldnt be here right now!
                        p = (string)tsp[3];

                        teaspWS = new teaspWithSub(t, ea, ds, p);
                        lt.Add(teaspWS);
                    } // When this is finished, lt will now have all the teasps that correspond to each value group ready

                    // We are now ready to build the extendedTeaspStorageManager

                    ETSM = new extendedTeaspStorageManager(ls, lt, dt);

                    // Add it to the dictionary
                    grandMasterDictionary.Add(conceptMappingTemplatesKeys[cKeyCounter], ETSM);
                    cKeyCounter++;
                }

            }

            foreach (object j in languageMappingTemplates)
            {
                castObjArray = (object[])j;

                if (castObjArray.Length == 1)
                {
                    object[] temp = (object[])castObjArray[0];
                    object[] teasp = (object[])temp[0];

                    t = (string)teasp[0];
                    ea = (string)teasp[1];
                    string s = (string)teasp[2];
                    p = (string)teasp[3];

                    teaspNS = new teaspNoSub(t, ea, s, p);
                    grandMasterDictionary.Add(languageMappingTemplatesKeys[lKeyCounter], teaspNS);
                    lKeyCounter++;

                }
                else if (castObjArray.Length != 0 && castObjArray.Length > 1) 
                {

                    object[] temp = (object[])castObjArray[0]; 
                    object[] deftsp = (object[])temp[0]; 
                    t = (string)deftsp[0];
                    ea = (string)deftsp[1];
                    string s = (string)deftsp[2]; 
                    p = (string)deftsp[3];

                    teaspNS = new teaspNoSub(t, ea, s, p);
                    dt = teaspNS;

                    temp = temp.Skip(1).ToArray(); 
                    foreach (string[] st in temp)
                    {
                        ls.Add(st); 
                    }

                    castObjArray = castObjArray.Skip(1).ToArray(); 
                    foreach (object[] tsp in castObjArray) 
                    {
                        t = (string)tsp[0];
                        ea = (string)tsp[1];
                        Dictionary<string, string> ds = (Dictionary<string, string>)tsp[2];
                        p = (string)tsp[3];

                        teaspWS = new teaspWithSub(t, ea, ds, p);
                        lt.Add(teaspWS);
                    } 

                    // We are now ready to build the extendedTeaspStorageManager

                    ETSM = new extendedTeaspStorageManager(ls, lt, dt);

                    // Add it to the dictionary
                    grandMasterDictionary.Add(languageMappingTemplatesKeys[lKeyCounter], ETSM);
                    lKeyCounter++;
                }

            }

            foreach (object j in termMappingTemplates)
            {
                castObjArray = (object[])j;

                if (castObjArray.Length == 1)
                {
                    object[] temp = (object[])castObjArray[0];
                    object[] teasp = (object[])temp[0];

                    t = (string)teasp[0];
                    ea = (string)teasp[1];
                    string s = (string)teasp[2];
                    p = (string)teasp[3];

                    teaspNS = new teaspNoSub(t, ea, s, p);
                    grandMasterDictionary.Add(termMappingTemplatesKeys[tKeyCounter], teaspNS);
                    tKeyCounter++;

                }
                else if (castObjArray.Length != 0 && castObjArray.Length > 1)
                {

                    object[] temp = (object[])castObjArray[0];
                    object[] deftsp = (object[])temp[0];
                    t = (string)deftsp[0];
                    ea = (string)deftsp[1];
                    string s = (string)deftsp[2];
                    p = (string)deftsp[3];

                    teaspNS = new teaspNoSub(t, ea, s, p);
                    dt = teaspNS;

                    temp = temp.Skip(1).ToArray();
                    foreach (string[] st in temp)
                    {
                        ls.Add(st);
                    }

                    castObjArray = castObjArray.Skip(1).ToArray();
                    foreach (object[] tsp in castObjArray)
                    {
                        t = (string)tsp[0];
                        ea = (string)tsp[1];
                        Dictionary<string, string> ds = (Dictionary<string, string>)tsp[2];
                        p = (string)tsp[3];

                        teaspWS = new teaspWithSub(t, ea, ds, p);
                        lt.Add(teaspWS);
                    }

                    // We are now ready to build the extendedTeaspStorageManager

                    ETSM = new extendedTeaspStorageManager(ls, lt, dt);

                    // Add it to the dictionary
                    grandMasterDictionary.Add(termMappingTemplatesKeys[tKeyCounter], ETSM);
                    tKeyCounter++;
                }

            }

        }

    }

    // The One-level mappings are broken down into 3 dictionaries, each belonging to one of the original concept levels, and then sent to parse the template-sets that are still JObjects at this point;

    public class oneLevelMapping
    {
        public Dictionary<string, object> cOLvlDictionary = new Dictionary<string, object> ();
        public Dictionary<string, object> lOLvlDictionary = new Dictionary<string, object> ();
        public Dictionary<string, object> tOLvlDictionary = new Dictionary<string, object> ();
        public templateSet ts;

        public oneLevelMapping(Dictionary<string, JObject> d)
        {
            JObject tempC = d["concept"];
            cOLvlDictionary = tempC.ToObject<Dictionary<string, object>>();

            JObject tempL = d["language"];
            lOLvlDictionary = tempL.ToObject<Dictionary<string, object>>();

            JObject tempT = d["term"];
            tOLvlDictionary = tempT.ToObject<Dictionary<string, object>>();
        }

        public Dictionary<string, object> beginTemplate()
        {
            ts = new templateSet(cOLvlDictionary, lOLvlDictionary, tOLvlDictionary);
            return ts.getGrandMasterDictionary();
        }
    }

    // A dictionary is created for the 3 possible categorical mappings: Concept, Language and Term. Their values are still JObjects that are handed off to the next function.

    public class cMapClass
    {
        public Dictionary<string, JObject> cDefault = new Dictionary<string, JObject> ();
        public oneLevelMapping passDictionary;
        public Dictionary<string, object> ds = new Dictionary<string, object> ();

        public cMapClass(JObject c, JObject l, JObject t)
        {
            cDefault.Add("concept", c);
            cDefault.Add("language", l);
            cDefault.Add("term", t); 
        }

        public Dictionary<string, object> parseOLvl() //Hand over dictionary to oneLevelMapping
        {
            passDictionary = new oneLevelMapping(cDefault);
            ds = passDictionary.beginTemplate();
            return ds;
        }

    }



    //////////

    // The orders are seperated into lists for each key that exists. This is the end of handling the Queue-draining orders

    public class listOfOrders
    {
        List<string[]> concept;
        List<string[]> language;
        List<string[]> term;
        int i = 0;

        public listOfOrders(Dictionary<string, JObject> k)
        {
            JObject t = k["conceptGrp"];
            string[][] s = t.ToObject<string[][]>();
            foreach(string[] a in s)
            {
                concept.Add(a);
            }      

            JObject t1 = k["languageGrp"];
            string[][] s1 = t1.ToObject<string[][]>();
            foreach (string[] a in s1)
            {
                language.Add(a);
            }

            JObject t2 = k["termGrp"];
            string[][] s2 = t2.ToObject<string[][]>();
            foreach (string[] a in s2)
            {
                term.Add(a);
            }

        }
    }

    // The beginning of the Queue-drainind orders method. The object is constructed with the JObject[3] sent from the original JObject. A dictionary is created and passed for parsing the orders

    public class queueOrders
    {
        public Dictionary<string, JObject> qBOrders = new Dictionary<string, JObject>(); //Or just a regular object?? 
        public listOfOrders loo;

        public queueOrders(JObject j)
        {
            JObject cGStrings = (JObject)j["conceptGrp"];
            JObject lGStrings = (JObject)j["languageGrp"];
            JObject tGStrings = (JObject)j["termGrp"];

            qBOrders.Add("conceptGrp", cGStrings);
            qBOrders.Add("languageGrp", lGStrings);
            qBOrders.Add("termGrp", tGStrings);
        }

        public void passDictionary()
        {
            loo = new listOfOrders(qBOrders);
        }
    }

    //////////




    // This is where the surface level JSON file is stored. The categorical mapping is parsed though the parseCMap method and the Queue-draining orders are parsed through the startQueue method

    public class levelOneClass 
    {
        public string dialect { get; set; }
        public string xcsElement { get; set; }
        public JArray objectStorage { get; set; }
        public cMapClass parseCMP;
        public queueOrders QDO;
        public Dictionary<string, object> dictionaryStorage = new Dictionary<string, object> ();

        public levelOneClass(string d, string x, JArray cmp)
        {
            dialect = d;
            xcsElement = x;
            objectStorage = cmp;
        }

        public string getDialect()
        {
            return dialect;
        }

        public string getXCS()
        {
            return xcsElement;
        }

        public void parseCMap()
        {
            JObject conceptLvl = (JObject)objectStorage[2]["concept"];
            JObject languageLvl = (JObject)objectStorage[2]["language"];
            JObject termLvl = (JObject)objectStorage[2]["term"];

            parseCMP = new cMapClass(conceptLvl, languageLvl, termLvl);
            dictionaryStorage = parseCMP.parseOLvl();
        }

        public void startQueue()
        {
            JObject j = (JObject)objectStorage[3];
            QDO = new queueOrders(j);
        }

        public Dictionary<string, object> getMasterDictionary()
        {
            return dictionaryStorage;
        }


    }

    // This is where the files are input and the parsing process is initiated

    public class Methods
    {
        public static List<string> globalOpenTags = new List<string> ();

        public static int findIndex(List<string[]> ValGrpTemp, string currentContent)
        {
            for (int i = 0; i < ValGrpTemp.Count(); i++)
            {
                string[] q = ValGrpTemp[i];
                for (int k = 0; k < q.Count(); k++)
                {
                    if (q[k] == currentContent)
                    {
                        // Remember this spot and break, dont store k because order may be different in teasp's substitution rule
                        return i; // This will indicate which index in the correspondingTemp List has our teasp    
                    }
                }
            }
            return -1; // Did not find content in Value-Groups, indicate that default must be used 
        }

        public static void startXMLImport(FileStream inXML, FileStream outXML, levelOneClass initialJSON)
        {
            XmlWriterSettings settingW = new XmlWriterSettings() { Indent = true, IndentChars = " " };
            XmlReaderSettings settingsR = new XmlReaderSettings();
            string d = initialJSON.getDialect(); // ****
            string x = initialJSON.getXCS();    // ****
            Dictionary<string, object> grandMasterD = new Dictionary<string, object> ();
            grandMasterD = initialJSON.getMasterDictionary();
            string storeAttribute;
            int teaspIndex = 0;

            string target;
            string element;
            string placement;
            string currentContent;
            string stringSub;
            Dictionary<string,string> stringOther = new Dictionary<string, string> ();
            string stringValue;

            List<string[]> ValGrpTemp;
            List<teaspWithSub> correspondTemp;
            teaspNoSub defaultTeaspSub;

            string storeAtt;
            string storeContent;
            string storeDateContent;


            using (XmlReader reader = XmlReader.Create(inXML, settingsR))
            {
                using (XmlWriter writer = XmlWriter.Create(outXML, settingW))
                {
                    writer.WriteStartDocument();
                    while (reader.Read())
                    {
                        if (reader.IsStartElement())
                        {
                            switch (reader.NodeType)
                            {
                                case XmlNodeType.Element:

                                    if (reader.Name == "mtf")
                                    {

                                        // Print boiler-plate TBX header 

                                        string langDeclaration = "xmllang";

                                        writer.WriteStartElement("martif");
                                        writer.WriteAttributeString("type", d); // Need to retrieve dialect from levelOneClass
                                        writer.WriteAttributeString(langDeclaration, "en");

                                        writer.WriteStartElement("martifHeader");
                                        writer.WriteStartElement("fileDesc");
                                        writer.WriteStartElement("sourceDesc");
                                        writer.WriteStartElement("p");
                                        writer.WriteString("Auto-converted from Multiterm XML");

                                        writer.WriteEndElement(); // Closes <p>

                                        writer.WriteEndElement(); // Closes <sourceDesc>

                                        writer.WriteEndElement(); // Closes <fileDesc>

                                        writer.WriteStartElement("encodingDesc");
                                        writer.WriteStartElement("p");
                                        writer.WriteAttributeString("type", "DCSName");
                                        writer.WriteString(x); // Need to retrieve XCS from levelOneClass

                                        writer.WriteEndElement(); // Closes <p>

                                        writer.WriteEndElement(); // Closes <encodingDesc>

                                        writer.WriteEndElement(); // Closes <martifHeader>

                                        writer.WriteStartElement("text");
                                        writer.WriteStartElement("body");

                                        globalOpenTags.Add("text");
                                        globalOpenTags.Add("body");
                                        globalOpenTags.Add("martif");

                                        // End boiler-plate TBX header

                                        break;

                                    }

                                    if (reader.Name == "language") // I think we'll need a special call for Local-codes that are lacking
                                    {
                                        storeAtt = reader.GetAttribute("lang");
                                        writer.WriteStartElement(reader.Name); // NOT DONE
                                        writer.WriteAttributeString("lang", storeAtt);
                                        writer.WriteEndElement();
                                        break;
                                    }

                                    if (reader.Name != "mtf" && reader.Name != "transac" && reader.HasAttributes) // We have found a winner!
                                    {
                                        storeAttribute = reader.GetAttribute("type");
                                        if(grandMasterD.ContainsKey(storeAttribute)) // Time to process
                                        {
                                            currentContent = reader.ReadElementContentAsString();
                                            // Determine typeof and cast

                                            if (grandMasterD[storeAttribute].GetType() == typeof(teaspNoSub)) // No Value Groups
                                            {
                                                teaspNoSub temp = (teaspNoSub)grandMasterD[storeAttribute];
                                                // From here we can just grab and go
                                                target = temp.getTarget();
                                                element = temp.getElementOrAttribute();
                                                stringSub = temp.getSubstitution();
                                                placement = temp.getPlacement();

                                                writer.WriteStartElement(reader.Name); // Write the Element currently beind handled
                                                // Grab the desired section of the attribute out of element with regex!
                                                Match match = Regex.Match(element, @"'([^']*)");
                                                string att = match.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                                writer.WriteAttributeString("type", att); // Give it its attributes
                                                writer.WriteString(currentContent);
                                                writer.WriteEndElement(); // Closes current Element

                                            }
                                            if (grandMasterD[storeAttribute].GetType() == typeof(extendedTeaspStorageManager)) // Has Value Groups
                                            {
                                                extendedTeaspStorageManager tempExt = (extendedTeaspStorageManager)grandMasterD[storeAttribute];
                                                // Step 1: Check each string[] in the List<string[]> for the content of the element
                                                ValGrpTemp = tempExt.getValueGroupCollection();
                                                correspondTemp = tempExt.getCorrespondingValGrpTeasps();

                                                teaspIndex = findIndex(ValGrpTemp, currentContent);

                                                // Step 2: Either fetch the teasp or use the default
                                                if (teaspIndex >= 0)
                                                {
                                                    teaspWithSub fetchTeasp = correspondTemp[teaspIndex];
                                                    target = fetchTeasp.getTarget();
                                                    element = fetchTeasp.getElementOrAttribute();
                                                    stringOther = fetchTeasp.getSubstitution();
                                                    placement = fetchTeasp.getPlacement();
                                                    stringValue = stringOther[currentContent];

                                                    // Ready to Write!
                                                    writer.WriteStartElement(reader.Name); // Write the Element currently beind handled
                                                    // Grab the desired section of the attribute out of element with regex!
                                                    Match match = Regex.Match(element, @"'([^']*)");
                                                    string att = match.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                                    writer.WriteAttributeString("type", att); // Give it its attributes
                                                    writer.WriteString(stringValue);
                                                    writer.WriteEndElement(); // Closes current Element

                                                }
                                                else if (teaspIndex == -1)
                                                {
                                                    defaultTeaspSub = tempExt.getDefaultTeaspSub();
                                                    target = defaultTeaspSub.getTarget();
                                                    element = defaultTeaspSub.getElementOrAttribute();
                                                    stringSub = defaultTeaspSub.getSubstitution();
                                                    placement = defaultTeaspSub.getPlacement();

                                                    // Ready to write!
                                                    writer.WriteStartElement(reader.Name); // Write the Element currently beind handled
                                                    // Grab the desired section of the attribute out of element with regex!
                                                    Match match = Regex.Match(element, @"'([^']*)");
                                                    string att = match.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                                    writer.WriteAttributeString("type", att); // Give it its attributes
                                                    writer.WriteString(currentContent);
                                                    writer.WriteEndElement(); // Closes current Element
                                                }

                                            }

                                        }
                                        break;
                                    }

                                    if (reader.Name == "transac") // Gotta hardcode this guy, doesnt appear in a JSON mapping file
                                    {
                                        storeAtt = reader.GetAttribute("type");
                                        storeContent = reader.ReadElementContentAsString();
                                        reader.ReadToNextSibling("date"); // ** ReadToNextSibling or ReadToFollowing???
                                        storeDateContent = reader.ReadElementContentAsString();

                                        writer.WriteStartElement("transacGrp");
                                        writer.WriteStartElement("transac");
                                        writer.WriteAttributeString("type", "transactionType");
                                        writer.WriteString(storeAtt);
                                        writer.WriteEndElement();

                                        writer.WriteStartElement("transacNote");
                                        writer.WriteAttributeString("type", "Responsability");
                                        writer.WriteString(storeContent);
                                        writer.WriteEndElement();

                                        writer.WriteStartElement("date");
                                        writer.WriteString(storeDateContent);
                                        writer.WriteEndElement();

                                        writer.WriteEndElement();
                                        break;
                                    }

                                    if (reader.Name != "mtf" && !(reader.HasAttributes)) // Plain-Jane Element
                                    {
                                        writer.WriteStartElement(reader.Name);
                                        globalOpenTags.Add(reader.Name);
                                        break;
                                    }

                                    break;

                                case XmlNodeType.EndElement: // Check to see if the current element is still open before closing anything

                                    foreach(string s in globalOpenTags)
                                    {
                                        if (s == reader.Name)
                                        {
                                            writer.WriteEndElement();
                                            globalOpenTags.Remove(reader.Name);
                                        }
                                    }
                                    break;

                            }
                        }
                    }
                }
            }
        }

        public static string readFile(string type) // Start Here
        {
            string fn = "";

            OpenFileDialog dlg = new OpenFileDialog();

            dlg.Title = "Please select your " + type + " file.";

            bool? result = dlg.ShowDialog();

            if (result == true)
            {
                fn = dlg.FileName;
            }

            return fn;
        }

        public static void deserializeFile(string filename1, string filename2)
        {
            string text = File.ReadAllText(filename1);

            JArray data = (JArray)JsonConvert.DeserializeObject(text);

            string dialect = (string)data[0];
            string xcs = (string)data[1];


            //Stores First two strings and the remainder of the data as an un-parsed object
            levelOneClass initialJSON = new levelOneClass(dialect, xcs, data);

            //Parses the file from the concept-map down to the bottom
            initialJSON.parseCMap();

            //Import XML File
            FileStream inXML = File.OpenRead(filename2);
            FileStream outXML = File.OpenWrite("ConvertedTBX.tbx");
            // FileStream orderXML = File.OpenWrite("OrderedTBX.tbx"); Keep the reorder method seperate, possibly close and reopen Converted file for only reading?

            startXMLImport(inXML, outXML, initialJSON);

            // reorderTBX(outXML, orderXML);

        }
    }
}



