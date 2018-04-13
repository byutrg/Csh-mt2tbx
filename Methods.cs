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
/// Currently the program is functional, and has no known current errors 
/// 
/// Known limitations:
/// 
/// ** Should any elements changed by the mapping file require being placed as a child of a new element, the program only knows how to place adminNotes in adminGrps
/// 
/// </summary>



namespace Csh_mt2tbx
{
    // This holds teasps that have one or more value groups. 

    public class extendedTeaspStorageManager
    {
        public List<string[]> valueGroupCollection; // Each string[] will correspond with the teasp in the same position
        public List<object> correspondingValGrpTeasps; // Every one of these will have substitutions, and therefore the default will not be in this list
        public teaspNoSub defaultTeaspSub;

        public extendedTeaspStorageManager(List<string[]> ls, List<object> lt, teaspNoSub dt)
        {
            valueGroupCollection = ls;
            correspondingValGrpTeasps = lt;
            defaultTeaspSub = dt;
        }

        public List<string[]> getValueGroupCollection()
        {
            return valueGroupCollection;
        }

        public List<object> getCorrespondingValGrpTeasps()
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

    // This holds teasps that have no value groups, but do have substitutions

    public class teaspWithSub
    {
        public string target;
        public string elementOrAttributes;
        public Dictionary<string, string> substitution = new Dictionary<string, string>();
        public string placement;

        public teaspWithSub(string t, string ea, Dictionary<string, string> s, string p)
        {
            target = t;
            elementOrAttributes = ea;
            foreach (KeyValuePair<string, string> entry in s)
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
        public List<object> conceptMappingTemplates = new List<object>();
        public List<string> conceptMappingTemplatesKeys = new List<string>();
        public List<object> languageMappingTemplates = new List<object>();
        public List<string> languageMappingTemplatesKeys = new List<string>();
        public List<object> termMappingTemplates = new List<object>();
        public List<string> termMappingTemplatesKeys = new List<string>();
        public object[] castObjArray;
        public string key;
        public teaspNoSub teaspNS;
        public teaspWithSub teaspWS;
        public int handler = 0;
        public int cKeyCounter = 0;
        public int lKeyCounter = 0;
        public int tKeyCounter = 0;
        public string[] valGrp;

        // // // //

        // This is the Dicitonary that will contain the Mapping Templates Strings and an object (Either a plain teasp or a extendedTeaspStorageManager object).
        // Regardless of what kind of object each Key-Value pair has, type will be determined at runtime and processing will be done then.

        public Dictionary<string, object> grandMasterDictionary = new Dictionary<string, object>();

        // // // //


        public string t;
        public string ea;
        // Declare s at runtime
        public string p;


        public templateSet(Dictionary<string, object> c, Dictionary<string, object> l, Dictionary<string, object> t)
        {
            foreach (var entry in c)
            {
                object tempUNK1 = entry.Value;
                conceptMappingTemplates.Add(tempUNK1);

                key = entry.Key;
                conceptMappingTemplatesKeys.Add(key);
            }
            foreach (var entry2 in l)
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

            foreach (JArray j in conceptMappingTemplates)
            {
                castObjArray = (object[])j.ToObject(typeof(object[]));

                if (castObjArray.Length == 1) // This is "plain" template set
                {
                    JArray tempJA = (JArray)castObjArray[0];
                    object[] teasp = (object[])tempJA.ToObject(typeof(object[]));
                    JArray plainTeasp = (JArray)teasp[0];

                    t = (string)plainTeasp[0].ToObject(typeof(string));
                    ea = (string)plainTeasp[1].ToObject(typeof(string));
                    p = (string)plainTeasp[3].ToObject(typeof(string));

                    // // //
                    JToken castTest = (JToken)plainTeasp[2].ToObject(typeof(JToken));
                    string castTestString = "";


                    try
                    {
                        castTestString = (string)castTest.ToObject(typeof(string));
                    }
                    catch (Exception e)
                    {
                        handler = 0;
                    }

                    try
                    {
                        Dictionary<string, string> castTestDictionary = (Dictionary<string, string>)castTest.ToObject(typeof(Dictionary<string, string>));
                    }
                    catch (Exception e)
                    {
                        handler = 1;
                    }

                    // // // Check casting here

                    if (handler == 0)
                    {
                        Dictionary<string, string> exceptionSub = (Dictionary<string, string>)plainTeasp[2].ToObject(typeof(Dictionary<string, string>));
                        var teaspMy = new teaspWithSub(t, ea, exceptionSub, p);

                        grandMasterDictionary.Add(conceptMappingTemplatesKeys[cKeyCounter], teaspMy);
                    }
                    else if (handler == 1)
                    {
                        string s = (string)plainTeasp[2].ToObject(typeof(string));
                        var teaspMy = new teaspNoSub(t, ea, s, p);

                        grandMasterDictionary.Add(conceptMappingTemplatesKeys[cKeyCounter], teaspMy);
                    }

                    // // //

                    cKeyCounter++;

                }
                else if (castObjArray.Length != 0 && castObjArray.Length > 1) // This is a template set with Value groups 
                {
                    List<string[]> ls0 = new List<string[]>();
                    List<object> lt0 = new List<object>();
                    teaspNoSub defTeasp0;

                    JArray temp = (JArray)castObjArray[0]; // This will have the default Teasp and subsequent value groups
                    object[] deftsp = (object[])temp.ToObject(typeof(object[])); // Grab the default teasp
                    JArray defaultTsp = (JArray)deftsp[0];

                    t = (string)defaultTsp[0].ToObject(typeof(string));
                    ea = (string)defaultTsp[1].ToObject(typeof(string));
                    string s = (string)defaultTsp[2].ToObject(typeof(string));
                    p = (string)defaultTsp[3].ToObject(typeof(string));

                    teaspNS = new teaspNoSub(t, ea, s, p); // This is now ready to give to the extendedTeaspStorageManager
                    defTeasp0 = teaspNS;

                    deftsp = deftsp.Skip(1).ToArray(); // We dont want the first array, it is its own teasp, this array now just has value groups
                    foreach (JArray st in deftsp)
                    {
                        string[] singleValGrp = (string[])st.ToObject(typeof(string[]));
                        ls0.Add(singleValGrp); // This populates the list of string[] for the extendedTeaspStorageManager with all value-groups
                    }

                    castObjArray = castObjArray.Skip(1).ToArray(); // We dont want the first array, because it will be handled seperately (above)
                    foreach (JArray tsp in castObjArray) // This handles the teasps that correspond with each value-group
                    {
                        t = (string)tsp[0].ToObject(typeof(string));
                        ea = (string)tsp[1].ToObject(typeof(string));
                        p = (string)tsp[3].ToObject(typeof(string));

                        // // //
                        JToken castTest = (JToken)tsp[2].ToObject(typeof(JToken));
                        string castTestString = "";


                        try
                        {
                            castTestString = (string)castTest.ToObject(typeof(string));
                        }
                        catch (Exception e)
                        {
                            handler = 0;
                        }

                        try
                        {
                            Dictionary<string, string> castTestDictionary = (Dictionary<string, string>)castTest.ToObject(typeof(Dictionary<string, string>));
                        }
                        catch (Exception e)
                        {
                            handler = 1;
                        }

                        // // // Check casting here

                        if (handler == 0)
                        {
                            Dictionary<string, string> exceptionSub = (Dictionary<string, string>)tsp[2].ToObject(typeof(Dictionary<string, string>));
                            var teaspMy = new teaspWithSub(t, ea, exceptionSub, p);

                            lt0.Add(teaspMy);
                        }
                        else if (handler == 1)
                        {
                            string str = (string)tsp[2].ToObject(typeof(string));
                            var teaspMy = new teaspNoSub(t, ea, s, p);

                            lt0.Add(teaspMy);
                        }
                    } // When this is finished, lt will now have all the teasps that correspond to each value group ready

                    // We are now ready to build the extendedTeaspStorageManager

                    extendedTeaspStorageManager ETSM1 = new extendedTeaspStorageManager(ls0, lt0, defTeasp0);

                    // Add it to the dictionary
                    grandMasterDictionary.Add(conceptMappingTemplatesKeys[cKeyCounter], ETSM1);
                    cKeyCounter++;
                }

            }

            foreach (JArray j in languageMappingTemplates)
            {
                castObjArray = (object[])j.ToObject(typeof(object[]));

                if (castObjArray.Length == 1)
                {
                    JArray tempJA = (JArray)castObjArray[0];
                    object[] teasp = (object[])tempJA.ToObject(typeof(object[]));
                    JArray plainTeasp = (JArray)teasp[0];

                    t = (string)plainTeasp[0].ToObject(typeof(string));
                    ea = (string)plainTeasp[1].ToObject(typeof(string));
                    p = (string)plainTeasp[3].ToObject(typeof(string));

                    // // //
                    JToken castTest = (JToken)plainTeasp[2].ToObject(typeof(JToken));
                    string castTestString = "";


                    try
                    {
                        castTestString = (string)castTest.ToObject(typeof(string));
                    }
                    catch (Exception e)
                    {
                        handler = 0;
                    }

                    try
                    {
                        Dictionary<string, string> castTestDictionary = (Dictionary<string, string>)castTest.ToObject(typeof(Dictionary<string, string>));
                    }
                    catch (Exception e)
                    {
                        handler = 1;
                    }

                    // // // Check casting here

                    if (handler == 0)
                    {
                        Dictionary<string, string> exceptionSub = (Dictionary<string, string>)plainTeasp[2].ToObject(typeof(Dictionary<string, string>));
                        var teaspMy = new teaspWithSub(t, ea, exceptionSub, p);

                        grandMasterDictionary.Add(languageMappingTemplatesKeys[lKeyCounter], teaspMy);
                    }
                    else if (handler == 1)
                    {
                        string s = (string)plainTeasp[2].ToObject(typeof(string));
                        var teaspMy = new teaspNoSub(t, ea, s, p);

                        grandMasterDictionary.Add(languageMappingTemplatesKeys[lKeyCounter], teaspMy);
                    }

                    // // //

                    lKeyCounter++;

                }
                else if (castObjArray.Length != 0 && castObjArray.Length > 1)
                {
                    List<string[]> ls2 = new List<string[]>();
                    List<object> lt2 = new List<object>();
                    teaspNoSub defTeasp2;


                    JArray temp = (JArray)castObjArray[0]; // This will have the default Teasp and subsequent value groups
                    object[] deftsp = (object[])temp.ToObject(typeof(object[])); // Grab the default teasp
                    JArray defaultTsp = (JArray)deftsp[0];

                    t = (string)defaultTsp[0].ToObject(typeof(string));
                    ea = (string)defaultTsp[1].ToObject(typeof(string));
                    string s = (string)defaultTsp[2].ToObject(typeof(string));
                    p = (string)defaultTsp[3].ToObject(typeof(string));

                    teaspNS = new teaspNoSub(t, ea, s, p); // This is now ready to give to the extendedTeaspStorageManager
                    defTeasp2 = teaspNS;

                    deftsp = deftsp.Skip(1).ToArray(); // We dont want the first array, it is its own teasp, this array now just has value groups
                    foreach (JArray st in deftsp)
                    {
                        string[] singleValGrp = (string[])st.ToObject(typeof(string[]));
                        ls2.Add(singleValGrp); // This populates the list of string[] for the extendedTeaspStorageManager with all value-groups
                    }

                    castObjArray = castObjArray.Skip(1).ToArray(); // We dont want the first array, because it will be handled seperately (above)
                    foreach (JArray tsp in castObjArray) // This handles the teasps that correspond with each value-group
                    {
                        t = (string)tsp[0].ToObject(typeof(string));
                        ea = (string)tsp[1].ToObject(typeof(string));
                        p = (string)tsp[3].ToObject(typeof(string));

                        // // //
                        JToken castTest = (JToken)tsp[2].ToObject(typeof(JToken));
                        string castTestString = "";


                        try
                        {
                            castTestString = (string)castTest.ToObject(typeof(string));
                        }
                        catch (Exception e)
                        {
                            handler = 0;
                        }

                        try
                        {
                            Dictionary<string, string> castTestDictionary = (Dictionary<string, string>)castTest.ToObject(typeof(Dictionary<string, string>));
                        }
                        catch (Exception e)
                        {
                            handler = 1;
                        }

                        // // // Check casting here

                        if (handler == 0)
                        {
                            Dictionary<string, string> exceptionSub = (Dictionary<string, string>)tsp[2].ToObject(typeof(Dictionary<string, string>));
                            var teaspMy = new teaspWithSub(t, ea, exceptionSub, p);

                            lt2.Add(teaspMy);
                        }
                        else if (handler == 1)
                        {
                            string str = (string)tsp[2].ToObject(typeof(string));
                            var teaspMy = new teaspNoSub(t, ea, s, p);

                            lt2.Add(teaspMy);
                        }

                    } // When this is finished, lt will now have all the teasps that correspond to each value group ready

                    // We are now ready to build the extendedTeaspStorageManager

                    extendedTeaspStorageManager ETSM2 = new extendedTeaspStorageManager(ls2, lt2, defTeasp2);

                    // Add it to the dictionary
                    grandMasterDictionary.Add(languageMappingTemplatesKeys[lKeyCounter], ETSM2);
                    lKeyCounter++;
                }

            }

            foreach (JArray j in termMappingTemplates)
            {
                castObjArray = (object[])j.ToObject(typeof(object[]));

                if (castObjArray.Length == 1)
                {
                    JArray tempJA = (JArray)castObjArray[0];
                    object[] teasp = (object[])tempJA.ToObject(typeof(object[]));
                    JArray plainTeasp = (JArray)teasp[0];

                    t = (string)plainTeasp[0].ToObject(typeof(string));
                    ea = (string)plainTeasp[1].ToObject(typeof(string));
                    p = (string)plainTeasp[3].ToObject(typeof(string));

                    // // //
                    JToken castTest = (JToken)plainTeasp[2].ToObject(typeof(JToken));
                    string castTestString = "";


                    try
                    {
                        castTestString = (string)castTest.ToObject(typeof(string));
                    }
                    catch (Exception e)
                    {
                        handler = 0;
                    }

                    try
                    {
                        Dictionary<string, string> castTestDictionary = (Dictionary<string, string>)castTest.ToObject(typeof(Dictionary<string, string>));
                    }
                    catch (Exception e)
                    {
                        handler = 1;
                    }

                    // // // Check casting here

                    if (handler == 0)
                    {
                        Dictionary<string, string> exceptionSub = (Dictionary<string, string>)plainTeasp[2].ToObject(typeof(Dictionary<string, string>));
                        var teaspMy = new teaspWithSub(t, ea, exceptionSub, p);

                        grandMasterDictionary.Add(termMappingTemplatesKeys[tKeyCounter], teaspMy);
                    }
                    else if (handler == 1)
                    {
                        string s = (string)plainTeasp[2].ToObject(typeof(string));
                        var teaspMy = new teaspNoSub(t, ea, s, p);

                        grandMasterDictionary.Add(termMappingTemplatesKeys[tKeyCounter], teaspMy);
                    }

                    // // //
                    tKeyCounter++;

                }
                else if (castObjArray.Length != 0 && castObjArray.Length > 1)
                {
                    List<string[]> ls3 = new List<string[]>();
                    List<object> lt3 = new List<object>();
                    teaspNoSub defTeasp3;

                    JArray temp = (JArray)castObjArray[0]; // This will have the default Teasp and subsequent value groups
                    object[] deftsp = (object[])temp.ToObject(typeof(object[])); // Grab the default teasp
                    JArray defaultTsp = (JArray)deftsp[0];

                    t = (string)defaultTsp[0].ToObject(typeof(string));
                    ea = (string)defaultTsp[1].ToObject(typeof(string));
                    string s = (string)defaultTsp[2].ToObject(typeof(string));
                    p = (string)defaultTsp[3].ToObject(typeof(string));

                    teaspNS = new teaspNoSub(t, ea, s, p); // This is now ready to give to the extendedTeaspStorageManager
                    defTeasp3 = teaspNS;

                    deftsp = deftsp.Skip(1).ToArray(); // We dont want the first array, it is its own teasp, this array now just has value groups
                    foreach (JArray st in deftsp)
                    {
                        string[] singleValGrp = (string[])st.ToObject(typeof(string[]));
                        ls3.Add(singleValGrp); // This populates the list of string[] for the extendedTeaspStorageManager with all value-groups
                    }

                    castObjArray = castObjArray.Skip(1).ToArray(); // We dont want the first array, because it will be handled seperately (above)
                    foreach (JArray tsp in castObjArray) // This handles the teasps that correspond with each value-group
                    {
                        t = (string)tsp[0].ToObject(typeof(string));
                        ea = (string)tsp[1].ToObject(typeof(string));
                        p = (string)tsp[3].ToObject(typeof(string));

                        // // //
                        JToken castTest = (JToken)tsp[2].ToObject(typeof(JToken));
                        string castTestString = "";


                        try
                        {
                            castTestString = (string)castTest.ToObject(typeof(string));
                        }
                        catch (Exception e)
                        {
                            handler = 0;
                        }

                        try
                        {
                            Dictionary<string, string> castTestDictionary = (Dictionary<string, string>)castTest.ToObject(typeof(Dictionary<string, string>));
                        }
                        catch (Exception e)
                        {
                            handler = 1;
                        }

                        // // // Check casting here

                        if (handler == 0)
                        {
                            Dictionary<string, string> exceptionSub = (Dictionary<string, string>)tsp[2].ToObject(typeof(Dictionary<string, string>));
                            var teaspMy = new teaspWithSub(t, ea, exceptionSub, p);

                            lt3.Add(teaspMy);
                        }
                        else if (handler == 1)
                        {
                            string str = (string)tsp[2].ToObject(typeof(string));
                            var teaspMy = new teaspNoSub(t, ea, s, p);

                            lt3.Add(teaspMy);
                        }
                    } // When this is finished, lt will now have all the teasps that correspond to each value group ready

                    // We are now ready to build the extendedTeaspStorageManager

                    extendedTeaspStorageManager ETSM3 = new extendedTeaspStorageManager(ls3, lt3, defTeasp3);

                    // Add it to the dictionary
                    grandMasterDictionary.Add(termMappingTemplatesKeys[tKeyCounter], ETSM3);
                    tKeyCounter++;
                }

            }

        }

    }

    // The One-level mappings are broken down into 3 dictionaries, each belonging to one of the original concept levels, and then sent to parse the template-sets that are still JObjects at this point;

    public class oneLevelMapping
    {
        public Dictionary<string, object> cOLvlDictionary = new Dictionary<string, object>();
        public Dictionary<string, object> lOLvlDictionary = new Dictionary<string, object>();
        public Dictionary<string, object> tOLvlDictionary = new Dictionary<string, object>();
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
        public Dictionary<string, JObject> cDefault = new Dictionary<string, JObject>();
        public oneLevelMapping passDictionary;
        public Dictionary<string, object> ds = new Dictionary<string, object>();

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
        List<string[]> concept = new List<string[]>();
        List<string[]> language = new List<string[]>();
        List<string[]> term = new List<string[]>();

        public listOfOrders(Dictionary<string, JArray> k)
        {
            JArray c = (JArray)k["conceptGrp"];
            object[] sArray1 = (object[])c.ToObject(typeof(object[]));
            string[][] s = c.ToObject<string[][]>();
            foreach (string[] a in s)
            {
                concept.Add(a);
            }

            JArray l = (JArray)k["languageGrp"];
            object[] sArray2 = (object[])l.ToObject(typeof(object[]));
            string[][] s1 = l.ToObject<string[][]>();
            foreach (string[] a in s1)
            {
                language.Add(a);
            }

            JArray t = (JArray)k["termGrp"];
            object[] sArray3 = (object[])t.ToObject(typeof(object[]));
            string[][] s2 = t.ToObject<string[][]>();
            foreach (string[] a in s2)
            {
                term.Add(a);
            }

        }

        public List<string[]> getConcerpt()
        {
            return concept;
        }

        public List<string[]> getLanugage()
        {
            return language;
        }

        public List<string[]> getTerm()
        {
            return term;
        }
    }

    // The beginning of the Queue-drainind orders method. The object is constructed with the JObject[3] sent from the original JObject. A dictionary is created and passed for parsing the orders

    public class queueOrders
    {
        public Dictionary<string, JArray> qBOrders = new Dictionary<string, JArray>(); //Or just a regular object?? 
        public listOfOrders loo;

        public queueOrders(JObject j)
        {
            JArray cGStrings = (JArray)j["conceptGrp"];
            JArray lGStrings = (JArray)j["languageGrp"];
            JArray tGStrings = (JArray)j["termGrp"];

            qBOrders.Add("conceptGrp", cGStrings);
            qBOrders.Add("languageGrp", lGStrings);
            qBOrders.Add("termGrp", tGStrings);
            loo = new listOfOrders(qBOrders);
        }

        public Dictionary<string, string[]> getOrders()
        {
            Dictionary<string, string[]> combinedOrders = new Dictionary<string, string[]>();
            List<string[]> c = loo.getConcerpt();
            List<string[]> l = loo.getLanugage();
            List<string[]> t = loo.getTerm();

            for (int i = 0; i < (c.Count()); i++)
            {
                // for each value in a string array, there needs to be a key with that string value, and a value of the array. In an array of 3 strings, you will have 3 keys and each will have the same value
                for (int j = 0; j < 2; j++)
                {
                    if (combinedOrders.ContainsKey(c[i][j]))
                    {
                        continue;
                    }
                    combinedOrders.Add(c[i][j], c[i]); // Make sure this isnt nonsense
                }
            }
            for (int i = 0; i < (l.Count()); i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (combinedOrders.ContainsKey(l[i][j]))
                    {
                        continue;
                    }
                    combinedOrders.Add(l[i][j], l[i]);
                }
            }
            for (int i = 0; i < (t.Count()); i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    if (combinedOrders.ContainsKey(t[i][j]))
                    {
                        continue;
                    }
                    combinedOrders.Add(t[i][j], t[i]);
                }
            }

            return combinedOrders;

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
        public Dictionary<string, object> dictionaryStorage = new Dictionary<string, object>();

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
            startQueue();
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

        public Dictionary<string, string[]> getQueueOrders()
        {
            return QDO.getOrders();
        }


    }

    // This is where the files are input and the parsing process is initiated

    public class Methods
    {
        public static void addColon(string pathToFile)
        {
            string text = File.ReadAllText(pathToFile);
            text = text.Replace("xmllang", "xml:lang");
            text = text.Replace("termGrp", "tig");
            text = text.Replace(" type=\"\"", "");
            string pattern = @"<descripGrp>[\n\w\d\s<=>\/\:\-\,\\\^\$\.\|\?\*\+\(\)\{\}\""âãäåæçèéêëìíîïðñòóôõøùúûüýþÿı\']*?<\/descripGrp>";
            MatchCollection matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                string originalCopy = match.Groups[0].Value;
                string childCheck = match.Groups[0].Value;
                int countTabs = 0;
                int countOpenTags = 0;

                Regex regexInner = new Regex(@"<descrip[^G]");
                Match matchInner = regexInner.Match(childCheck);

                if (!matchInner.Success)
                {
                    foreach (char c in childCheck)
                    {
                        if (c == '\t')
                        {
                            countTabs++;
                        }
                        if (c == '<')
                        {
                            countOpenTags++;
                        }
                        if (countOpenTags > 1)
                        {
                            break;
                        }
                    }
                    string replacementWithTabs = "<descrip type=\"otherBinaryData\">see next element</descrip>";
                    for (int i = 0; i < countTabs; i++)
                    {
                        replacementWithTabs = "\t" + replacementWithTabs;
                    }
                    replacementWithTabs = "<descripGrp>\r\n" + replacementWithTabs;
                    childCheck = childCheck.Replace("<descripGrp>", replacementWithTabs);
                    text = text.Replace(originalCopy, childCheck);
                }
            }
            string pattern2 = @"<[\n\w\d\s<=>\:\-\,\\\^\$\.\|\?\*\+\(\)\{\}âãäåæçèéêëìíîïðñòóôõøùúûüýþÿ\""\']*?>\t+";
            MatchCollection matches2 = Regex.Matches(text, pattern2, RegexOptions.IgnoreCase);
            foreach (Match match2 in matches2)
            {
                string original = match2.Groups[0].Value;
                string change = match2.Groups[0].Value;

                change = change.Replace("\t", "");
                text = text.Replace(original, change);
            }
            File.WriteAllText(pathToFile, text);
        }

        public static void finalRecursion(XmlNode n, XmlWriter writer9)
        {
            XmlNode termPosition = null;
            XmlNode termNoteNode = null;
            XmlNode auxInfoNode = null;

            // First move the necessary node(s)

            for (int i = 0; i < n.ChildNodes.Count; i++)
            {
                if (n.ChildNodes[i].Name == "term")
                {
                    termPosition = n.ChildNodes[i];
                }

                if (n.ChildNodes[i].Name == "termNote")
                {
                    termNoteNode = n.ChildNodes[i];
                    n.InsertAfter(termNoteNode, termPosition); // Should throw error if not term is found
                }

                if (n.Name == "langSet" && n.ChildNodes[i].Name != "termGrp" && n.ChildNodes[i].NodeType != XmlNodeType.Whitespace) // Move auxinfo to top of langSet
                {
                    auxInfoNode = n.ChildNodes[i];
                    n.PrependChild(auxInfoNode);
                }

            }

            // Second, recursively print nodes

            if (n.Attributes["type"] != null)
            {
                string storeAtt = n.Attributes["type"].Value;
                writer9.WriteStartElement(n.Name);
                writer9.WriteAttributeString("type", storeAtt);
            }
            else if (n.Attributes["lang"] != null)
            {
                writer9.WriteStartElement(n.Name);
                writer9.WriteAttributeString("lang", n.Attributes["lang"].Value);
            }
            else if (n.Attributes["id"] != null)
            {
                writer9.WriteStartElement(n.Name);
                writer9.WriteAttributeString("id", n.Attributes["id"].Value);
            }
            else if (n.Attributes["xmllang"] != null)
            {
                writer9.WriteStartElement(n.Name);
                writer9.WriteAttributeString("xmllang", n.Attributes["xmllang"].Value);
            }
            else
            {
                writer9.WriteStartElement(n.Name);
            }

            if (n.HasChildNodes && n.ChildNodes.Count > 1) // Dangerous bet: If it just has text it will only have 1 child, otherwise it should have more?
            {
                for (int u = 0; u < n.ChildNodes.Count; u++)
                {
                    if (n.ChildNodes[u].NodeType == XmlNodeType.Whitespace)
                    {
                        continue;
                    }

                    if (n.ChildNodes[u].NodeType == XmlNodeType.Element)
                    {
                        if (n.ChildNodes[u].Attributes["type"] != null && n.ChildNodes[u].Attributes["type"].Value.Contains("FLAG")) // All flagged tags should come through here
                        {
                            if (n.ChildNodes[u].Attributes["type"].Value.Contains("annotatedNoteFLAG"))
                            {
                                writer9.WriteStartElement("adminGrp");
                                if (n.ChildNodes[u].Name == "note")
                                {
                                    writer9.WriteStartElement("admin");
                                }
                                else
                                {
                                    writer9.WriteStartElement(n.ChildNodes[u].Name);
                                }
                                writer9.WriteAttributeString("type", "annotatedNote");
                                writer9.WriteString(n.ChildNodes[u].InnerText);
                                writer9.WriteEndElement();

                                writer9.WriteStartElement(n.ChildNodes[u + 2].Name);
                                writer9.WriteAttributeString("type", n.ChildNodes[u + 2].Attributes["type"].Value);
                                writer9.WriteString(n.ChildNodes[u + 2].InnerText);
                                writer9.WriteEndElement();
                                writer9.WriteEndElement();
                                u += 2;
                                continue;
                            }
                        }
                        XmlNode k = n.ChildNodes[u];
                        finalRecursion(k, writer9);
                    }

                    if (n.ChildNodes[u].NodeType == XmlNodeType.Text)
                    {
                        string holdText = n.ChildNodes[u].InnerText;
                        if (holdText.Contains("\t"))
                        {
                            holdText = holdText.Replace("\t", "");
                        }
                        writer9.WriteString(holdText);
                        continue;
                    }

                    if (n.ChildNodes[u].NodeType == XmlNodeType.EndElement)
                    {
                        writer9.WriteEndElement();
                        continue;
                    }
                }
            }
            else
            {
                string holdText = n.InnerText;
                if (holdText.Contains("\t"))
                {
                    holdText = holdText.Replace("\t", "");
                }
                writer9.WriteString(holdText);
            }

            writer9.WriteEndElement();
        }

        public static void finalProcesses(FileStream finalIn, FileStream outFinal)
        {
            XmlDocument doc9 = new XmlDocument();
            XmlReaderSettings settingsNewR9 = new XmlReaderSettings();
            settingsNewR9.DtdProcessing = DtdProcessing.Parse;
            XmlWriterSettings settingNewW9 = new XmlWriterSettings() { Indent = true, IndentChars = "\t" };

            using (XmlReader reader9 = XmlReader.Create(finalIn, settingsNewR9))
            {
                using (XmlWriter writer9 = XmlWriter.Create(outFinal, settingNewW9))
                {
                    writer9.WriteStartDocument();
                    writer9.WriteDocType("martif", null, "TBXcoreStructV02.dtd", null); // DocType Declaration
                    while (reader9.Read())
                    {
                        switch (reader9.NodeType)
                        {
                            case XmlNodeType.Whitespace:
                                break;

                            case XmlNodeType.Element:
                                if (reader9.HasAttributes && reader9.Name != "langSet")
                                {
                                    if (reader9.GetAttribute("type") != null && reader9.GetAttribute("type").Contains("FLAG")) // All flagged tags should come through here
                                    {
                                        if (reader9.GetAttribute("type").Contains("annotatedNoteFLAG"))
                                        {
                                            writer9.WriteStartElement("adminGrp");
                                            writer9.WriteStartElement(reader9.Name);
                                            writer9.WriteAttributeString("type", "annotatedNote");
                                            writer9.WriteString(reader9.ReadElementContentAsString());
                                            writer9.WriteEndElement();
                                            reader9.Read();
                                            writer9.WriteStartElement(reader9.Name);
                                            writer9.WriteAttributeString("type", reader9.GetAttribute("type"));
                                            writer9.WriteString(reader9.ReadElementContentAsString());
                                            writer9.WriteEndElement();
                                            writer9.WriteEndElement();
                                        }
                                        break;
                                    }

                                    writer9.WriteStartElement(reader9.Name);
                                    if (reader9.GetAttribute("type") != null)
                                    {
                                        writer9.WriteAttributeString("type", reader9.GetAttribute("type"));
                                    }
                                    if (reader9.GetAttribute("lang") != null)
                                    {
                                        writer9.WriteAttributeString("lang", reader9.GetAttribute("lang"));
                                    }
                                    if (reader9.GetAttribute("id") != null)
                                    {
                                        writer9.WriteAttributeString("id", reader9.GetAttribute("id"));
                                    }
                                    if (reader9.GetAttribute("xmllang") != null)
                                    {
                                        writer9.WriteAttributeString("xmllang", reader9.GetAttribute("xmllang"));
                                    }
                                    if (reader9.GetAttribute("multimedia") != null)
                                    {
                                        writer9.WriteAttributeString("multimedia", reader9.GetAttribute("multimedia"));
                                    }
                                    if (reader9.Name == "xref" && reader9.GetAttribute("target") == null)
                                    {
                                        reader9.Read();
                                        writer9.WriteAttributeString("target", reader9.Value);
                                        writer9.WriteString("Graphic");
                                    }
                                    if (reader9.Name == "martif")
                                    {
                                        writer9.WriteAttributeString("xmllang", "en");
                                    }
                                }
                                else if (reader9.Name == "langSet")
                                {
                                    // Move termNotes
                                    XmlNode n = doc9.ReadNode(reader9);
                                    finalRecursion(n, writer9);
                                }
                                else
                                {
                                    writer9.WriteStartElement(reader9.Name);
                                }
                                break;

                            case XmlNodeType.Text:
                                string holdText = reader9.Value;
                                if (holdText.Contains("\t"))
                                {
                                    holdText = holdText.Replace("\t", "");
                                }
                                writer9.WriteString(holdText);
                                break;

                            case XmlNodeType.EndElement:
                                writer9.WriteEndElement();
                                break;

                        }
                    }
                }
            }
        }

        public static void prettyPreProcess(FileStream preXML, FileStream PreStreamOut)
        {
            XmlReaderSettings settingsNewR0 = new XmlReaderSettings();
            XmlWriterSettings settingNewW0 = new XmlWriterSettings() { Indent = true, IndentChars = "\t" };

            using (XmlReader reader0 = XmlReader.Create(preXML, settingsNewR0))
            {
                using (XmlWriter writer0 = XmlWriter.Create(PreStreamOut, settingNewW0))
                {
                    writer0.WriteStartDocument();
                    while (reader0.Read())
                    {
                        switch (reader0.NodeType)
                        {
                            case XmlNodeType.Whitespace:
                                break;

                            case XmlNodeType.Element:
                                if (reader0.HasAttributes)
                                {
                                    writer0.WriteStartElement(reader0.Name);
                                    if (reader0.GetAttribute("lang") != null)
                                    {
                                        writer0.WriteAttributeString("lang", reader0.GetAttribute("lang"));
                                    }
                                    if (reader0.GetAttribute("type") != null)
                                    {
                                        writer0.WriteAttributeString("type", reader0.GetAttribute("type"));
                                    }
                                    if (reader0.GetAttribute("multimedia") != null)
                                    {
                                        writer0.WriteAttributeString("multimedia", reader0.GetAttribute("multimedia"));
                                    }
                                }
                                else
                                {
                                    writer0.WriteStartElement(reader0.Name);
                                }
                                break;


                            case XmlNodeType.Text:
                                writer0.WriteString(reader0.Value);
                                break;

                            case XmlNodeType.EndElement:
                                writer0.WriteEndElement();
                                break;

                        }
                    }
                }
            }
        }

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

        public static void recursivePrinter(XmlNode n, XmlWriter writer0)
        {
            int textCount = 0;
            int elementCount = 0;
            int nonWhiteCounter = 0;
            string scrapeAllText;

            if (n.Name == "language")
            {
                writer0.WriteStartElement(n.Name);
                writer0.WriteAttributeString("lang", n.Attributes["lang"].Value);
                writer0.WriteAttributeString("type", n.Attributes["type"].Value);
                writer0.WriteString("TEMPORARY CONTENT");
                writer0.WriteEndElement();
                return;
            }

            for (int p = 0; p < n.ChildNodes.Count; p++)
            {
                if (n.ChildNodes[p].NodeType != XmlNodeType.Whitespace)
                {
                    nonWhiteCounter++;
                }
            }

            if (nonWhiteCounter > 0) // Filter out emptied descripGrp tags
            {
                writer0.WriteStartElement(n.Name);
            }
            else
            {
                return;
            }

            if (n.Attributes["type"] != null)
            {
                writer0.WriteAttributeString("type", n.Attributes["type"].Value);
            }

            if (n.Attributes["multimedia"] != null)
            {
                writer0.WriteAttributeString("multimedia", n.Attributes["multimedia"].Value);
            }

            if ((n.ChildNodes.Count == 3 && n.ChildNodes[1].NodeType == XmlNodeType.Text) || (n.ChildNodes.Count == 1 && n.ChildNodes[0].NodeType == XmlNodeType.Text) || (n.ChildNodes.Count == 3 && n.ChildNodes[1].NodeType == XmlNodeType.Element && n.ChildNodes[2].NodeType == XmlNodeType.Text)) // Whitespace, Text, Whitespace
            {
                scrapeAllText = n.InnerText;

                if (scrapeAllText.Contains("\n"))
                {
                    scrapeAllText = Regex.Replace(scrapeAllText, @"\n", "");
                }
                writer0.WriteString(scrapeAllText);
            }
            else
            {
                for (int k = 0; k < n.ChildNodes.Count; k++)
                {
                    if (n.ChildNodes[k].NodeType == XmlNodeType.Element)
                    {
                        elementCount++;
                    }
                    else if (n.ChildNodes[k].NodeType == XmlNodeType.Text)
                    {
                        textCount++;
                    }
                }

                if (textCount >= 1 && elementCount >= 1) // Both Text and Elements were found, must contain xrefs, scrape, print and move on
                {
                    scrapeAllText = n.InnerText;

                    if (scrapeAllText.Contains("\n"))
                    {
                        scrapeAllText = Regex.Replace(scrapeAllText, @"\n", "");
                    }
                    writer0.WriteString(scrapeAllText);
                    writer0.WriteEndElement();
                    return;
                }
            }


            if (n.ChildNodes.Count == 3 && n.ChildNodes[1].NodeType == XmlNodeType.Element && n.ChildNodes[2].NodeType == XmlNodeType.Text)
            {
                writer0.WriteEndElement();
                return;
            }

            if (n.HasChildNodes)
            {
                for (int i = 0; i < n.ChildNodes.Count; i++)
                {
                    if (n.ChildNodes[i].NodeType != XmlNodeType.Whitespace && n.ChildNodes[i].NodeType != XmlNodeType.Text)
                    {
                        recursivePrinter(n.ChildNodes[i], writer0);
                    }
                }
            }

            writer0.WriteEndElement();
        }

        public static void recursiveDescent(Dictionary<string, string[]> masterQueueOrders, XmlNode n, XmlDocument docA)
        {
            if (n.HasChildNodes)
            {
                // Scanning Loop
                for (int k = 0; k < n.ChildNodes.Count; k++)
                {
                    if (n.ChildNodes[k].NodeType == XmlNodeType.Whitespace || n.ChildNodes[k].NodeType == XmlNodeType.Text)
                    {
                        continue;
                    }

                    if (n.Attributes["multimedia"] != null)
                    {
                        for (int i = 0; i < n.ChildNodes.Count; i++)
                        {
                            if (n.ChildNodes[i].NodeType == XmlNodeType.Whitespace)
                            {
                                continue;
                            }

                            if (n.ChildNodes[i].Attributes["type"] != null && n.ChildNodes[i].Attributes["type"].Value == "Graphic")
                            {
                                XmlAttribute newAtt = docA.CreateAttribute("multimedia");
                                newAtt.Value = n.Attributes["multimedia"].Value;
                                n.ChildNodes[i].Attributes.Append(newAtt);
                            }
                        }
                    }

                    if (n.ChildNodes[k].Attributes["type"] != null && masterQueueOrders.ContainsKey(n.ChildNodes[k].Attributes["type"].Value))
                    {
                        string storeElementName = n.ChildNodes[k].Name;
                        string storeEncounteredBundle = n.ChildNodes[k].Attributes["type"].Value;
                        string encounteredBundleText = n.ChildNodes[k].InnerText;
                        string[] currentBundleArray = masterQueueOrders[storeEncounteredBundle];
                        string storeCompanionBundle;
                        string companionBundleText;
                        if (currentBundleArray[0] == storeEncounteredBundle)
                        {
                            storeCompanionBundle = currentBundleArray[1];
                        }
                        else
                        {
                            storeCompanionBundle = currentBundleArray[0];
                        }
                        string storeBundleDirections = currentBundleArray[2];
                        bool foundCompanionBundle = false;

                        int indexOfChange = 0;
                        XmlNode nextNode;
                        XmlNode foundPartner = null;


                        // 1) Check the sibling nodes for the other value or try 2)

                        for (int u = k; u < n.ChildNodes.Count; u++)
                        {
                            if (n.ChildNodes[u].NodeType == XmlNodeType.Whitespace)
                            {
                                continue;
                            }

                            if (n.ChildNodes[u].Attributes["type"] != null && n.ChildNodes[u].Attributes["type"].Value == storeCompanionBundle)
                            {
                                companionBundleText = n.ChildNodes[u].InnerText;
                                indexOfChange = u; // Come to this spot to remove the info later
                                foundPartner = n.ChildNodes[u];
                                n.RemoveChild(n.ChildNodes[u]);
                                foundCompanionBundle = true;
                            }
                        }

                        // 2) Grab parentNode and call nextSibling and check its children until we've found the matching att or die

                        if (foundCompanionBundle == false)
                        {
                            nextNode = n;
                            while (((nextNode = nextNode.NextSibling) != null) && foundCompanionBundle == false)
                            {
                                for (int v = 0; v < nextNode.ChildNodes.Count; v++)
                                {
                                    if (nextNode.ChildNodes[v].NodeType == XmlNodeType.Whitespace)
                                    {
                                        continue;
                                    }

                                    if (nextNode.ChildNodes[v].Attributes["type"] != null && nextNode.ChildNodes[v].Attributes["type"].Value == storeCompanionBundle)
                                    {
                                        companionBundleText = nextNode.ChildNodes[v].InnerText;
                                        indexOfChange = v; // Come to this spot to remove the info later
                                        foundPartner = nextNode.ChildNodes[v];
                                        nextNode.RemoveChild(nextNode.ChildNodes[v]);
                                        foundCompanionBundle = true;
                                    }
                                }
                            }
                            if (foundCompanionBundle == false)
                            {
                                // Ran out of siblings and still didn't find it... not sure where to look now!
                            }
                        }


                        // 3) Process the changes with XmlNode Methods and Properties
                        // Interesting discovery: The mapping file should make all the necessary changes with the name and att, I might just need to put the right nodes in the right place?

                        if (foundCompanionBundle == true)
                        {
                            n.InsertAfter(foundPartner, n.ChildNodes[k]);
                        }

                    }
                    else
                    {
                        if (n.ChildNodes[k].HasChildNodes)
                        {
                            recursiveDescent(masterQueueOrders, n.ChildNodes[k], docA);
                        }
                    }
                }
            }
        }

        public static void queueInjection(FileStream preXML, FileStream postXML, levelOneClass initialJSON)
        {
            XmlReaderSettings settingsNewR = new XmlReaderSettings();
            XmlWriterSettings settingNewW = new XmlWriterSettings() { Indent = true, IndentChars = "\t" };
            XmlDocument docA = new XmlDocument();
            Dictionary<string, string[]> m = initialJSON.getQueueOrders();

            using (XmlReader reader0 = XmlReader.Create(preXML, settingsNewR))
            {
                using (XmlWriter writer0 = XmlWriter.Create(postXML, settingNewW))
                {
                    writer0.WriteStartDocument();
                    while (reader0.Name != "mtf")
                    {
                        reader0.Read();
                    }
                    XmlNode alpha = docA.ReadNode(reader0);
                    recursiveDescent(m, alpha, docA);

                    recursivePrinter(alpha, writer0);
                }
            }
        }

        public static void printBoilerPlate(XmlWriter writer, string x, string d)
        {
            string langDeclaration = "xmllang";

            writer.WriteStartElement("martif");
            writer.WriteAttributeString("type", d); // Need to retrieve dialect from levelOneClass
            writer.WriteAttributeString(langDeclaration, "en");

            writer.WriteStartElement("martifHeader");
            writer.WriteStartElement("fileDesc");
            writer.WriteStartElement("sourceDesc");
            writer.WriteStartElement("p");
            writer.WriteString("Auto-converted from MultiTerm XML");

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
        }

        public static void startXMLImport(FileStream inXML, FileStream outXML, levelOneClass initialJSON)
        {
            XmlWriterSettings settingW = new XmlWriterSettings() { Indent = true, IndentChars = "\t" };
            settingW.ConformanceLevel = ConformanceLevel.Auto;
            XmlReaderSettings settingsR = new XmlReaderSettings();
            string d = initialJSON.getDialect();
            string x = initialJSON.getXCS();    
            Dictionary<string, object> grandMasterD = new Dictionary<string, object>();
            grandMasterD = initialJSON.getMasterDictionary();
            string storeAttribute;
            int teaspIndex = 0;

            string target = "";
            string element = "";
            string placement = "";
            string currentContent = "";
            string stringSub = "";
            Dictionary<string, string> stringOther = new Dictionary<string, string>();
            string stringValue = "";

            List<string[]> ValGrpTemp;
            List<object> correspondTemp;
            teaspNoSub defaultTeaspSub;
            teaspNoSub castWithout;
            teaspWithSub castWith;
            List<string> xrefPairs = new List<string>();
            XmlDocument doc = new XmlDocument();
            List<int> usedNodesAndChildren = new List<int>();
            List<int> everythingElse = new List<int>();
            List<int> onlyChildLocation = new List<int>();
            List<XmlNode> onlyChild = new List<XmlNode>();

            Dictionary<string, string[]> masterQueueOrders = initialJSON.getQueueOrders();

            string storeAtt = "";
            string storeSecondAtt;
            string storeContent = "";
            string storeDateContent = "";
            string scrapeAllText;

            using (XmlReader reader = XmlReader.Create(inXML, settingsR))
            {
                using (XmlWriter writer = XmlWriter.Create(outXML, settingW))
                {
                    writer.WriteStartDocument();
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:

                                if (reader.Name == "mtf")
                                {
                                    // Print boiler-plate TBX header 
                                    printBoilerPlate(writer, x, d);
                                    break;

                                }  // Complete, Acceptable Cyclomatic Complexity

                                if (reader.HasAttributes && reader.GetAttribute("multimedia") != null && reader.Name == "descripGrp")
                                {
                                    writer.WriteStartElement(reader.Name);
                                    break;
                                } // Complete, Acceptable Cyclomatic Complexity

                                if (reader.Name == "language") 
                                {
                                    XmlNode f = doc.ReadNode(reader);

                                    storeAtt = f.Attributes["lang"].Value;
                                    storeSecondAtt = f.Attributes["type"].Value;
                                    writer.WriteStartElement(f.Name); // NOT DONE
                                    writer.WriteAttributeString("lang", storeAtt);
                                    writer.WriteAttributeString("type", storeSecondAtt);
                                    writer.WriteString("TEMPORARY CONTENT");
                                    writer.WriteEndElement();

                                    if (reader.NodeType == XmlNodeType.Element)
                                    {
                                        writer.WriteStartElement(reader.Name);
                                    }
                                    else if (reader.NodeType == XmlNodeType.EndElement)
                                    {
                                        writer.WriteEndElement();
                                    }

                                    break;
                                }  // Complete, Acceptable Cyclomatic Complexity

                                if (reader.Name != "mtf" && reader.Name != "transac" && reader.HasAttributes && reader.GetAttribute("type") != null) // We have found a winner!
                                {
                                    storeAttribute = reader.GetAttribute("type");
                                    if (grandMasterD.ContainsKey(storeAttribute)) // Time to process
                                    {
                                        string saveMedia = "";
                                        bool foundMedia = false;
                                        string saveNodeName = reader.Name;
                                        if (reader.GetAttribute("multimedia") != null)
                                        {
                                            saveMedia = reader.GetAttribute("multimedia");
                                            foundMedia = true;
                                        }
                                        XmlNode node = doc.ReadNode(reader);
                                        scrapeAllText = node.InnerText;

                                        // Clean-up the text for new-lines
                                        if (scrapeAllText.Contains("\n"))
                                        {
                                            scrapeAllText = Regex.Replace(scrapeAllText, @"\n", "");
                                        }

                                        currentContent = scrapeAllText;
                                        if (currentContent.Contains("|"))
                                        {
                                            string patt = @"\|[\w\d\s]*";
                                            Match match = Regex.Match(currentContent, patt);
                                            string saveBadText = match.Groups[0].Value;
                                            currentContent = currentContent.Replace(saveBadText, "");
                                        }

                                        // Determine typeof and cast

                                        if (grandMasterD[storeAttribute].GetType() == typeof(teaspNoSub)) // No Value Groups
                                        {
                                            teaspNoSub temp = (teaspNoSub)grandMasterD[storeAttribute];
                                            // From here we can just grab and go
                                            target = temp.getTarget();
                                            element = temp.getElementOrAttribute();
                                            stringSub = temp.getSubstitution();
                                            placement = temp.getPlacement();

                                            // The node name may very well be the same, but for safety, we will check what element is supposed to hold the info in question
                                            Match match1 = Regex.Match(element, @"<\w([^\s]+)");
                                            string hold = match1.Groups[0].Value;
                                            hold = hold.Substring(1);

                                            if (hold != saveNodeName)
                                            {
                                                saveNodeName = hold;
                                            }


                                            writer.WriteStartElement(saveNodeName); // Write the Element currently beind handled
                                            // Grab the desired section of the attribute out of element with regex!
                                            Match match2 = Regex.Match(element, @"'([^']*)");
                                            string att = match2.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                            writer.WriteAttributeString("type", att); // Give it its attributes
                                            if (foundMedia)
                                            {
                                                writer.WriteAttributeString("multimedia", saveMedia);
                                            }
                                            writer.WriteString(currentContent);

                                            // When we store the element content, we advance the reader and so the end tag is skipped, so we must close the tag now
                                            writer.WriteEndElement();
                                            if (reader.NodeType == XmlNodeType.Element)
                                            {
                                                writer.WriteStartElement(reader.Name);
                                            }
                                            else if (reader.NodeType == XmlNodeType.EndElement)
                                            {
                                                writer.WriteEndElement();
                                            }
                                        }
                                        if (grandMasterD[storeAttribute].GetType() == typeof(teaspWithSub))
                                        {
                                            teaspWithSub temp = (teaspWithSub)grandMasterD[storeAttribute];
                                            // From here we can just grab and go
                                            target = temp.getTarget();
                                            element = temp.getElementOrAttribute();
                                            stringOther = temp.getSubstitution();
                                            placement = temp.getPlacement();
                                            stringValue = stringOther[currentContent];

                                            // The node name may very well be the same, but for safety, we will check what element is supposed to hold the info in question
                                            Match match1 = Regex.Match(element, @"<\w([^\s]+)");
                                            string hold = match1.Groups[0].Value;
                                            hold = hold.Substring(1);

                                            if (hold != saveNodeName)
                                            {
                                                saveNodeName = hold;
                                            }

                                            writer.WriteStartElement(saveNodeName); // Write the Element currently beind handled
                                            // Grab the desired section of the attribute out of element with regex!
                                            Match match2 = Regex.Match(element, @"'([^']*)");
                                            string att = match2.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                            writer.WriteAttributeString("type", att); // Give it its attributes
                                            if (foundMedia)
                                            {
                                                writer.WriteAttributeString("multimedia", saveMedia);
                                            }
                                            writer.WriteString(stringValue);

                                            // When we store the element content, we advance the reader and so the end tag is skipped, so we must close the tag now
                                            writer.WriteEndElement();
                                            if (reader.NodeType == XmlNodeType.Element)
                                            {
                                                writer.WriteStartElement(reader.Name);
                                            }
                                            else if (reader.NodeType == XmlNodeType.EndElement)
                                            {
                                                writer.WriteEndElement();
                                            }
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
                                                object fetchTeasp = correspondTemp[teaspIndex];

                                                if (fetchTeasp is teaspWithSub)
                                                {
                                                    castWith = (teaspWithSub)fetchTeasp;
                                                    target = castWith.getTarget();
                                                    element = castWith.getElementOrAttribute();
                                                    stringOther = castWith.getSubstitution();
                                                    placement = castWith.getPlacement();
                                                    stringValue = stringOther[currentContent];
                                                }
                                                else if (fetchTeasp is teaspNoSub)
                                                {
                                                    castWithout = (teaspNoSub)fetchTeasp;
                                                    target = castWithout.getTarget();
                                                    element = castWithout.getElementOrAttribute();
                                                    stringValue = castWithout.getSubstitution();
                                                    placement = castWithout.getPlacement();
                                                }

                                                // The node name may very well be the same, but for safety, we will check what element is supposed to hold the info in question
                                                Match match1 = Regex.Match(element, @"<\w([^\s]+)");
                                                string hold = match1.Groups[0].Value;
                                                hold = hold.Substring(1);

                                                if (hold != saveNodeName)
                                                {
                                                    saveNodeName = hold;
                                                }

                                                // Ready to Write!
                                                writer.WriteStartElement(saveNodeName); // Write the Element currently beind handled
                                                // Grab the desired section of the attribute out of element with regex!
                                                Match match = Regex.Match(element, @"'([^']*)");
                                                string att = match.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                                writer.WriteAttributeString("type", att); // Give it its attributes
                                                if (foundMedia)
                                                {
                                                    writer.WriteAttributeString("multimedia", saveMedia);
                                                }
                                                writer.WriteString(stringValue);

                                                // When we store the element content, we advance the reader and so the end tag is skipped, so we must close the tag now
                                                writer.WriteEndElement();
                                                if (reader.NodeType == XmlNodeType.Element)
                                                {
                                                    writer.WriteStartElement(reader.Name);
                                                }
                                                else if (reader.NodeType == XmlNodeType.EndElement)
                                                {
                                                    writer.WriteEndElement();
                                                }
                                            }
                                            else if (teaspIndex == -1)
                                            {
                                                defaultTeaspSub = tempExt.getDefaultTeaspSub();
                                                target = defaultTeaspSub.getTarget();
                                                element = defaultTeaspSub.getElementOrAttribute();
                                                stringSub = defaultTeaspSub.getSubstitution();
                                                placement = defaultTeaspSub.getPlacement();

                                                // The node name may very well be the same, but for safety, we will check what element is supposed to hold the info in question
                                                Match match1 = Regex.Match(element, @"<\w([^\s]+)");
                                                string hold = match1.Groups[0].Value;
                                                hold = hold.Substring(1);

                                                if (hold != saveNodeName)
                                                {
                                                    saveNodeName = hold;
                                                }

                                                // Ready to write!
                                                writer.WriteStartElement(saveNodeName); // Write the Element currently beind handled
                                                // Grab the desired section of the attribute out of element with regex!
                                                Match match = Regex.Match(element, @"'([^']*)");
                                                string att = match.Groups[1].Value; // Not checking for errors here which hopefully isnt a problem

                                                writer.WriteAttributeString("type", att); // Give it its attributes
                                                if (reader.GetAttribute("multimedia") != null)
                                                {
                                                    writer.WriteAttributeString("multimedia", reader.GetAttribute("multimedia"));
                                                }
                                                writer.WriteString(currentContent);

                                                // When we store the element content, we advance the reader and so the end tag is skipped, so we must close the tag now
                                                writer.WriteEndElement();
                                                if (reader.NodeType == XmlNodeType.Element)
                                                {
                                                    writer.WriteStartElement(reader.Name);
                                                }
                                                else if (reader.NodeType == XmlNodeType.EndElement)
                                                {
                                                    writer.WriteEndElement();
                                                }
                                            }
                                        }
                                    }
                                    break;
                                } // Complete, High Cyclomatic Complexity (Bad)

                                if (reader.Name == "transacGrp") // Gotta hardcode this guy, doesnt appear in a JSON mapping file
                                {

                                    while (reader.Name != "transac")
                                    {
                                        reader.Read();
                                    }
                                    storeAtt = reader.GetAttribute("type");
                                    storeContent = reader.ReadElementContentAsString();

                                    while (reader.Name != "date")
                                    {
                                        reader.Read();
                                    }
                                    storeDateContent = reader.ReadElementContentAsString();

                                    writer.WriteStartElement("transacGrp");

                                    writer.WriteStartElement("transac");
                                    writer.WriteAttributeString("type", "transactionType");
                                    writer.WriteString(storeAtt);
                                    writer.WriteEndElement();

                                    writer.WriteStartElement("transacNote");
                                    writer.WriteAttributeString("type", "responsibility");
                                    writer.WriteString(storeContent);
                                    writer.WriteEndElement();

                                    writer.WriteStartElement("date");
                                    writer.WriteString(storeDateContent);
                                    writer.WriteEndElement();

                                    if (!(reader.NodeType == XmlNodeType.Whitespace))
                                    {
                                        writer.WriteEndElement();
                                    }

                                    break;
                                }  // Complete, Acceptable Cyclomatic Complexity

                                if (reader.Name != "mtf" && !(reader.HasAttributes)) // Plain-Jane Element
                                {
                                    writer.WriteStartElement(reader.Name);
                                    break;
                                } // Complete, Acceptable Cyclomatic Complexity

                                break;

                            case XmlNodeType.EndElement:
                                writer.WriteEndElement();
                                break;

                            case XmlNodeType.Text:
                                writer.WriteString(reader.Value);
                                break;

                        }
                    }

                    // File has finished Reading, Close leftover tags from header
                    bool success = true;
                    while (success)
                    {
                        try
                        {
                            writer.WriteEndElement();
                        }
                        catch (Exception e)
                        {
                            success = false;
                        }
                    }

                    // Wrap it up!
                    writer.Close();
                }
            }
        }

        public static void descendAndPrint(XmlNode g, XmlWriter writer2)
        {
            string storeAtt = "";
            bool unwritten = false;
            int depthTracker = 0;
            int descripCounter = 0;


            if (g.Attributes["type"] != null && g.Name != "descripGrp") // Hold off on descripGrp until we know if it has multiple children
            {
                storeAtt = g.Attributes["type"].Value;
                writer2.WriteStartElement(g.Name);
                writer2.WriteAttributeString("type", storeAtt);
            }
            else
            {
                if (g.Name != "descripGrp")
                {
                    writer2.WriteStartElement(g.Name);
                }
            }

            if (g.HasChildNodes && g.ChildNodes.Count > 1) // Dangerous bet: If it just has text it will only have 1 child, otherwise it should have more?
            {
                for (int i = 0; i < g.ChildNodes.Count; i++)
                {

                    if (g.ChildNodes[i].NodeType == XmlNodeType.Whitespace)
                    {
                        continue;
                    }

                    if (g.ChildNodes[i].NodeType == XmlNodeType.Element) // This should act as a shield to any whitespace or text values that would otherwise appear
                    {
                        if (g.ChildNodes[i].Name == "descripGrp")
                        {
                            XmlNode deeper = g.ChildNodes[i]; // Made of the node of the current child, we need to look at its children

                            // NEW ADDITION: Scan for destructive Nodes

                            for (int y = 0; y < deeper.ChildNodes.Count; y++)  // As of right now the only encountered destructive nodes are: adminNote
                            {
                                if (deeper.ChildNodes[y].Name == "adminNote") // Collect the Node and it's bundling partner
                                {
                                    XmlNode findPartner;
                                    XmlNode destructiveNode = deeper.ChildNodes[y];

                                    if (deeper.ChildNodes[y - 2].Name == "note" || deeper.ChildNodes[y - 2].Name == "admin")
                                    {
                                        findPartner = deeper.ChildNodes[y - 2];
                                    }
                                    else if (deeper.ChildNodes[y + 2].Name == "note" || deeper.ChildNodes[y + 2].Name == "admin")
                                    {
                                        findPartner = deeper.ChildNodes[y + 2];
                                    }
                                    else
                                    {
                                        throw new System.InvalidOperationException("Cannot Find appropraite bundle for misplaced tag"); // Hope this never happens
                                    }

                                    // deeper.RemoveChild(deeper.ChildNodes[y]);
                                    // deeper.RemoveChild(findPartner);


                                    // We cannot create a new node from scratch, so flag the pair to be renamed when writing

                                    findPartner.Attributes["type"].Value = "annotatedNoteFLAG";
                                    // Blow-up the Node it was inside
                                    deeper.RemoveAll();

                                    g.AppendChild(findPartner);
                                    g.AppendChild(destructiveNode);
                                }
                            }

                            for (int j = 0; j < deeper.ChildNodes.Count; j++)
                            {
                                descripCounter = 0;

                                if (deeper.ChildNodes[j].NodeType == XmlNodeType.Whitespace)
                                {
                                    continue;
                                }

                                if (deeper.ChildNodes[j].Name == "descrip" || deeper.ChildNodes[j].Name == "admin" || deeper.ChildNodes[j].Name == "termNote" || deeper.ChildNodes[j].Name == "note")
                                {
                                    unwritten = true;
                                    if (deeper.ChildNodes[j].Name == "termNote")
                                    {
                                        writer2.WriteStartElement("termNote");
                                        writer2.WriteAttributeString("type", deeper.ChildNodes[j].Attributes["type"].Value);
                                        writer2.WriteString(deeper.ChildNodes[j].InnerText);
                                        writer2.WriteEndElement();
                                        continue;
                                    }

                                    string name = deeper.ChildNodes[j].Name;
                                    string attribute = deeper.ChildNodes[j].Attributes["type"].Value;
                                    string text = deeper.ChildNodes[j].InnerText;

                                    if (deeper.ChildNodes[j + 2] == null) // Check 2 ahead for more elements, 1 for whitespace, 1 to arrive at next possible element
                                    { // The admin or descrip does not need to be in the descripGrp tag, and will be output without it
                                        writer2.WriteStartElement(name);
                                        if (attribute != "")
                                        {
                                            writer2.WriteAttributeString("type", attribute);
                                        }
                                        writer2.WriteString(text);
                                        writer2.WriteEndElement();
                                        j++;
                                        continue;
                                    }
                                    else // There is more inside this descrip, so write what we picked up and let it keep reading
                                    {
                                        for (int y = 0; y < deeper.ChildNodes.Count; y++)
                                        {
                                            if (deeper.ChildNodes[y].Name == "descrip")
                                            {
                                                descripCounter++;
                                            }
                                        }

                                        if (descripCounter > 1)
                                        {
                                            writer2.WriteStartElement(name);
                                            writer2.WriteAttributeString("type", attribute);
                                            writer2.WriteString(text);
                                            writer2.WriteEndElement();
                                            // skip depthTracker bc we dont want the descripGrp tag
                                        }
                                        else
                                        {
                                            if (depthTracker == 0)
                                            {
                                                writer2.WriteStartElement("descripGrp");
                                            }
                                            writer2.WriteStartElement(name);
                                            writer2.WriteAttributeString("type", attribute);
                                            writer2.WriteString(text);
                                            writer2.WriteEndElement();
                                            depthTracker++;
                                        }
                                    }
                                }
                                else
                                {
                                    if (unwritten == false)
                                    {
                                        writer2.WriteStartElement("descripGrp");
                                    }
                                    XmlNode p = deeper.ChildNodes[j];
                                    descendAndPrint(p, writer2);
                                }
                            }
                            if (depthTracker > 0)
                            {
                                writer2.WriteEndElement();
                                depthTracker = 0;
                            }
                        }
                        else
                        {
                            XmlNode k = g.ChildNodes[i];
                            descendAndPrint(k, writer2);
                        }
                    }

                    if (g.ChildNodes[i].NodeType == XmlNodeType.Text)
                    {
                        writer2.WriteString(g.ChildNodes[i].InnerText);
                    }

                    if (g.ChildNodes[i].NodeType == XmlNodeType.EndElement)
                    {
                        writer2.WriteEndElement();
                        continue;
                    }

                }
            }
            else
            {
                writer2.WriteString(g.InnerText);
            }


            writer2.WriteEndElement();

        }

        public static void reorderTBX(FileStream inOutXML, FileStream orderTBX)
        {
            XmlReaderSettings settingsNewR = new XmlReaderSettings();
            XmlWriterSettings settingNewW = new XmlWriterSettings() { Indent = true, IndentChars = "\t" };
            XmlDocument doc2 = new XmlDocument();
            string storeatt;
            string storeID;

            using (XmlReader reader2 = XmlReader.Create(inOutXML, settingsNewR))
            {
                using (XmlWriter writer2 = XmlWriter.Create(orderTBX, settingNewW))
                {
                    writer2.WriteStartDocument();
                    writer2.WriteDocType("martif", null, "TBXcoreStructV03.dtd", null); // DocType Declaration
                    while (reader2.Read())
                    {
                        switch (reader2.NodeType)
                        {
                            case XmlNodeType.Element:

                                if (reader2.Name == "termGrp")
                                {
                                    // termGrp will need to be changed
                                    // Check for termNotes that need to be first after term
                                    // #Recursion Magic

                                    XmlNode n = doc2.ReadNode(reader2); // n now has the entire termGrp and all work must be done with n, since reader2 has moved on to the next element after the entire termGrp and its children
                                    descendAndPrint(n, writer2);
                                    break;
                                }

                                if (reader2.Name == "descripGrp")
                                {
                                    reader2.Read();
                                    while (reader2.NodeType == XmlNodeType.Whitespace)
                                    {
                                        reader2.Read();
                                    }
                                    if (reader2.Name == "descrip" || reader2.Name == "admin" || reader2.Name == "termNote" || reader2.Name == "note" || reader2.Name == "xref")
                                    {
                                        if (reader2.Name == "termNote")
                                        {
                                            // Should not occur outside termGrp
                                        }

                                        string name = reader2.Name;
                                        string attribute = reader2.GetAttribute("type");
                                        string saveMedia = "";
                                        bool foundMedia = false;
                                        if (reader2.Name == "xref" && reader2.GetAttribute("multimedia") != null)
                                        {
                                            saveMedia = reader2.GetAttribute("multimedia");
                                            foundMedia = true;
                                        }
                                        string text = reader2.ReadElementContentAsString();

                                        reader2.Read();
                                        while (reader2.NodeType == XmlNodeType.Whitespace)
                                        {
                                            reader2.Read();
                                        }

                                        if (reader2.Name == "descripGrp" && reader2.NodeType == XmlNodeType.EndElement)
                                        { // The admin or descrip does not need to be in the descripGrp tag, and will be output without it
                                            writer2.WriteStartElement(name);
                                            if (attribute != "")
                                            {
                                                writer2.WriteAttributeString("type", attribute);
                                            }
                                            if (foundMedia)
                                            {
                                                writer2.WriteAttributeString("multimedia", saveMedia);
                                            }
                                            writer2.WriteString(text);
                                            writer2.WriteEndElement();
                                            reader2.Read();
                                            break;
                                        }
                                        else // There is more inside this descrip, so write what we picked up and let it keep reading
                                        {
                                            writer2.WriteStartElement("descripGrp");
                                            writer2.WriteStartElement(name);
                                            writer2.WriteAttributeString("type", attribute);
                                            if (foundMedia)
                                            {
                                                writer2.WriteAttributeString("multimedia", saveMedia);
                                            }
                                            writer2.WriteString(text);
                                            writer2.WriteEndElement();
                                            writer2.WriteStartElement(reader2.Name);
                                            if (reader2.GetAttribute("type") != null)
                                            {
                                                writer2.WriteAttributeString("type", reader2.GetAttribute("type"));
                                            }
                                        }

                                        break;
                                    }
                                    else
                                    {
                                        writer2.WriteStartElement("descripGrp");
                                        break;
                                    }

                                }

                                if (reader2.Name == "languageGrp") // LEAVE HERE
                                {
                                    reader2.Read(); // Whitespace
                                    reader2.Read(); // Language

                                    storeatt = reader2.GetAttribute("lang");

                                    writer2.WriteStartElement("langSet");
                                    writer2.WriteAttributeString("xmllang", storeatt);

                                    reader2.Read(); // Text
                                    reader2.Read(); // End Tag
                                    break;
                                }

                                if (reader2.Name == "conceptGrp") // LEAVE HERE
                                {
                                    // We need the Text Value of the concept child, so we must manually advance the reader
                                    reader2.Read(); // Whitespace
                                    reader2.Read(); // Concept
                                    reader2.Read(); // Text

                                    storeID = reader2.Value;
                                    storeID = "_" + storeID;

                                    writer2.WriteStartElement("termEntry");
                                    writer2.WriteAttributeString("id", storeID);

                                    reader2.Read(); // Advance one more so we have cleared the section and will continue to the next element beyond the concept closing tag.
                                    break;
                                }

                                if (reader2.HasAttributes)
                                {
                                    storeatt = reader2.GetAttribute("type");
                                    writer2.WriteStartElement(reader2.Name);
                                    writer2.WriteAttributeString("type", storeatt);
                                    break;
                                }

                                if (!(reader2.HasAttributes)) // We'll probably need several cases for when to not accept the element above this
                                {
                                    writer2.WriteStartElement(reader2.Name);
                                    break;
                                }

                                break;

                            case XmlNodeType.EndElement:
                                writer2.WriteEndElement();
                                break;

                            case XmlNodeType.Text:
                                writer2.WriteString(reader2.Value);
                                break;
                        }
                    }
                }
            }

        }

        public static string readFile(string type) // Cyclic 
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
            string injectFile;
            string prettyFile;
            string finalLocation;

            JArray data = (JArray)JsonConvert.DeserializeObject(text);

            string dialect = (string)data[0];
            string xcs = (string)data[1];


            //Stores First two strings and the remainder of the data as an un-parsed object
            levelOneClass initialJSON = new levelOneClass(dialect, xcs, data);

            //Parses the file from the concept-map down to the bottom
            initialJSON.parseCMap();

            //Import XML File
            FileStream preXML = File.OpenRead(filename2);
            string removeFile = System.IO.Path.GetFileName(filename2);
            prettyFile = filename2.Replace(removeFile, "prettyXML.xml");
            injectFile = filename2.Replace(removeFile, "InjectedTBX.tbx");
            finalLocation = filename2.Replace(removeFile, "FinalizedTBX.tbx");
            removeFile = filename2.Replace(removeFile, "ConveretedTBX.tbx");
            FileStream prePostXML = File.Create(prettyFile);
            FileStream postXML = File.Create(injectFile);
            FileStream outXML = File.Create(removeFile);
            FileStream outFinal = File.Create(finalLocation);
            string outFileName = removeFile.Replace("ConveretedTBX.tbx", "OrderedTBX.tbx");
            FileStream orderXML = File.Create(outFileName); // Keep the reorder method seperate, possibly close and reopen Converted file for only reading?

            // Pre-Pre-Process for Broken XML files
            prettyPreProcess(preXML, prePostXML);

            // Close for writing
            prePostXML.Close();

            FileStream preInXML = File.OpenRead(prettyFile);

            // Pre-Process for Queue-Bundling Orders 
            queueInjection(preInXML, postXML, initialJSON);

            // Close postXML for Writing
            postXML.Close();

            FileStream inXML = File.OpenRead(injectFile);

            // Initial Processing
            startXMLImport(inXML, outXML, initialJSON); // FINISHED

            // Close outXML for Writing
            outXML.Close();

            // Reopen for reading
            FileStream inOutXML = File.OpenRead(removeFile);

            reorderTBX(inOutXML, orderXML);

            // Close orderXML for writing
            orderXML.Close();

            FileStream finalIn = File.OpenRead(outFileName); // Problem here

            finalProcesses(finalIn, outFinal);

            // Close final file
            outFinal.Close();

            addColon(finalLocation);

            // Close a bunch of Files 
            preXML.Close();
        }
    }
}



