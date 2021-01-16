﻿using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Xml.Linq;

namespace josm_rule_generator
{
    class Program
    {
        public Stopwatch stopWatch = new Stopwatch();

        public List<string> title_list = new List<string>();
        public List<Page> pages = new List<Page>();

        static void Main(string[] args)
        {
            var instance = new Program();

            instance.GatherData();
        }

        public void GatherData()
        {
            try
            {
                GatherCategory("Category:Key descriptions", "Key:");
                GatherCategory("Category:Tag descriptions", "Tag:");

                stopWatch.Reset();
                stopWatch.Start();

                //Serialization
                System.Xml.Serialization.XmlSerializer writer = new System.Xml.Serialization.XmlSerializer(typeof(List<Page>));

                System.IO.FileStream file = System.IO.File.Create("pages.xml");

                writer.Serialize(file, pages);
                file.Close();

                stopWatch.Stop();

                Console.Write("Wiki pages exported to XML, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading wiki pages into temporary database: " + "\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void GatherCategory(string CategoryLabel, string LabelPrefix)
        {
            try
            {
                stopWatch.Reset();
                stopWatch.Start();

                GetCategoryContent(CategoryLabel);

                stopWatch.Stop();

                Console.Write("Categories gathered, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error gathering categories: " + "\n" + e.Message + "\n" + e.StackTrace);
            }

            try
            {
                stopWatch.Reset();
                stopWatch.Start();

                foreach (string title in title_list.FindAll(f => f.StartsWith(LabelPrefix)))
                    GetPageContent(title);

                stopWatch.Stop();

                Console.Write("Page sources gathered, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error gathering page sources: " + "\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void GetCategoryContent(string CategoryLabel)
        {
            RestClient _client = new RestClient();

            _client.BaseUrl = new Uri("https://wiki.openstreetmap.org/w/");

            RestRequest request = new RestRequest("api.php", Method.GET);

            request.AddParameter("action", "query");
            request.AddParameter("format", "xml");
            request.AddParameter("list", "categorymembers");
            request.AddParameter("cmtitle", CategoryLabel);
            request.AddParameter("cmprop", "ids|title|type");
            request.AddParameter("cmtype", "subcat|file|page");
            request.AddParameter("cmlimit", "max");

            IRestResponse _response = _client.Execute(request);

            TextReader tr = new StringReader(_response.Content);
            XDocument _xDoc = XDocument.Load(tr);

            foreach (XElement element in _xDoc.Descendants())
            {
                if (element.Attribute("type") != null && element.Attribute("title") != null)
                    if (element.Attribute("type").Value == "page")
                        if (!(title_list.Contains(element.Attribute("title").Value)))
                            title_list.Add(element.Attribute("title").Value);
                if (element.Attribute("type") != null && element.Attribute("title") != null)
                    if (element.Attribute("type").Value == "subcat")
                        GetCategoryContent(element.Attribute("title").Value);
            }

            _client.ClearHandlers();
        }

        public void GetPageContent(string Title)
        {
            RestClient _client = new RestClient();

            _client.BaseUrl = new Uri("https://wiki.openstreetmap.org/w/");

            RestRequest request = new RestRequest("api.php", Method.GET);

            request.AddParameter("action", "query");
            request.AddParameter("format", "xml");
            request.AddParameter("prop", "revisions");
            request.AddParameter("titles", Title);
            request.AddParameter("rvprop", "content");
            request.AddParameter("rvslots", "*");

            IRestResponse _response = _client.Execute(request);

            TextReader tr = new StringReader(_response.Content);
            XDocument _xDoc = XDocument.Load(tr);

            foreach (XElement element in _xDoc.Descendants())
                if (element.Name.LocalName == "slot")
                    pages.Add(new Page { title = Title, value = element.Value });

            _client.ClearHandlers();
        }
    }
}
