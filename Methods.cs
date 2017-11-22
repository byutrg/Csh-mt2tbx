using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

    // This is the class for the first object in a template-set that contains the default teasp and value-groups

    public class defaultAndValGroup
    {
        public teasp defaultTeasp;
        public string[] valueGroup1;
        public string[] valueGroup2;

        public defaultAndValGroup(string[][] dAndV)
        {
            defaultTeasp = new teasp(dAndV[0]);
            valueGroup1 = dAndV[1];
            valueGroup2 = dAndV[2];
        }

    }

    // This is the vanilla teasp template. It takes an array of strings as its constructor and stores the appropriate values, although it currently does not account for the possibility of a 
    // dictionary-like object for a substitution.

    public class teasp
    {
        public string target;
        public string elementOrAttributes;
        public string substitution; //Could be a string or Dictionary<string, string>?
        public string placement;

        public teasp(string[] teaspinfo)
        {
            target = teaspinfo[0];
            elementOrAttributes = teaspinfo[1];
            substitution = teaspinfo[2];
            placement = teaspinfo[3];
        } 

    }

    // The template set is constructed by seperating all of the template-set jObjects from the dictionaries, and stored in individual Lists. Each list is then parsed and seperated into 
    // the default teasp and value-groups, followed by the subsequent teasps.

    public class templateSet
    {
        public List<JObject> conceptMappingTemplates;
        public List<JObject> languageMappingTemplates;
        public List<JObject> termMappingTemplates;
        public object[] defteaspteasp;
        public defaultAndValGroup DV; 
        public teasp teasp1;
        public teasp teasp2;


        public templateSet(Dictionary<string, JObject> c, Dictionary<string, JObject> l, Dictionary<string, JObject> t)
        {
            foreach(var entry in c)
            {
                JObject tempUNK1 = entry.Value;
                conceptMappingTemplates.Add(tempUNK1);
            }
            foreach(var entry2 in l)
            {
                JObject tempUNK2 = entry2.Value;
                languageMappingTemplates.Add(tempUNK2);
            }
            foreach (var entry3 in t)
            {
                JObject tempUNK3 = entry3.Value;
                termMappingTemplates.Add(tempUNK3);
            }
        }


        public void convertTemplateSets() 
        {
            foreach(JObject j in conceptMappingTemplates)
            {
                defteaspteasp = j.ToObject<object[]>();


                string[][] d1 = (string[][])defteaspteasp[0];
                string[] t1 = (string[])defteaspteasp[1];
                string[] t2 = (string[])defteaspteasp[2];

                DV = new defaultAndValGroup(d1);
                teasp1 = new teasp(t1);
                teasp2 = new teasp(t2);

            }

            foreach (JObject j in languageMappingTemplates)
            {
                defteaspteasp = j.ToObject<object[]>();


                string[][] d1 = (string[][])defteaspteasp[0];
                string[] t1 = (string[])defteaspteasp[1];
                string[] t2 = (string[])defteaspteasp[2];

                DV = new defaultAndValGroup(d1);
                teasp1 = new teasp(t1);
                teasp2 = new teasp(t2);

            }

            foreach (JObject j in termMappingTemplates)
            {
                defteaspteasp = j.ToObject<object[]>();


                string[][] d1 = (string[][])defteaspteasp[0];
                string[] t1 = (string[])defteaspteasp[1];
                string[] t2 = (string[])defteaspteasp[2];

                DV = new defaultAndValGroup(d1);
                teasp1 = new teasp(t1);
                teasp2 = new teasp(t2);

            }

        }

    }

    // The One-level mappings are broken down into 3 dictionaries, each belonging to one of the original concept levels, and then sent to parse the template-sets that are still JObjects at this point;

    public class oneLevelMapping
    {
        public Dictionary<string, JObject> cOLvlDictionary = new Dictionary<string, JObject> ();
        public Dictionary<string, JObject> lOLvlDictionary = new Dictionary<string, JObject> ();
        public Dictionary<string, JObject> tOLvlDictionary = new Dictionary<string, JObject> ();
        public templateSet ts;

        public oneLevelMapping(Dictionary<string, JObject> d)
        {
            JObject tempC = d["concept"];
            cOLvlDictionary = tempC.ToObject<Dictionary<string, JObject>>();

            JObject tempL = d["language"];
            lOLvlDictionary = tempL.ToObject<Dictionary<string, JObject>>();

            JObject tempT = d["term"];
            tOLvlDictionary = tempT.ToObject<Dictionary<string, JObject>>();
        }

        public void beginTemplate()
        {
            ts = new templateSet(cOLvlDictionary, lOLvlDictionary, tOLvlDictionary);
        }
    }

    // A dictionary is created for the 3 possible categorical mappings: Concept, Language and Term. Their values are still JObjects that are handed off to the next function.

    public class cMapClass
    {
        public Dictionary<string, JObject> cDefault = new Dictionary<string, JObject> ();
        public oneLevelMapping passDictionary;

        public cMapClass(JObject c, JObject l, JObject t)
        {
            cDefault.Add("concept", c);
            cDefault.Add("language", l);
            cDefault.Add("term", t); 
        }

        public void parseOLvl() //Hand over dictionary to oneLevelMapping
        {
            passDictionary = new oneLevelMapping(cDefault);
            passDictionary.beginTemplate();
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

        public levelOneClass(string d, string x, JArray cmp)
        {
            dialect = d;
            xcsElement = x;
            objectStorage = cmp;
        }

        public void parseCMap()
        {
            JObject conceptLvl = (JObject)objectStorage[2]["concept"];
            JObject languageLvl = (JObject)objectStorage[2]["language"];
            JObject termLvl = (JObject)objectStorage[2]["term"];

            parseCMP = new cMapClass(conceptLvl, languageLvl, termLvl);
            parseCMP.parseOLvl();
        }

        public void startQueue()
        {
            JObject j = (JObject)objectStorage[3];
            QDO = new queueOrders(j);
        }


    }

    // This is where the files are input and the parsing process is initiated

    public class Methods
    {
        public static string readFile(string type)
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

        public static string deserializeFile(string filename)
        {
            string text = File.ReadAllText(fn);

            JArray data = (JArray)JsonConvert.DeserializeObject(text);

            string dialect = (string)data[0];
            string xcs = (string)data[1];


            //Stores First two strings and the remainder of the data as an un-parsed object
            levelOneClass initialJSON = new levelOneClass(dialect, xcs, data);

            //Parses the file from the concept-map down to the bottom
            initialJSON.parseCMap();    
        }
    }
}



