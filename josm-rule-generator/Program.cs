using RestSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace josm_rule_generator
{
    class Program
    {
        public SQLiteConnection sqlConn = new SQLiteConnection("Data Source=osm_wiki.db;Version=3;");

        public string sql;
        public string sql_col_list;
        public string sql_values;

        public int start_pos;
        public int close_pos;

        public string wiki_desc;
        public string _parameters;

        public List<String> parameters;

        public List<string> title_list = new List<string>();
        public List<Page> pages = new List<Page>();

        public string key;
        public string value;

        public SQLiteCommand command;
        public Stopwatch stopWatch = new Stopwatch();
        public SQLiteTransaction transaction;

        public SQLiteDataReader reader;

        static void Main(string[] args)
        {
            var instance = new Program();

            instance.Generate();
        }

        public void Generate()
        {
            try
            {
                ConnectTempDb();

                stopWatch.Reset();
                stopWatch.Start();

                sql = "DELETE FROM wiki_pages";
                command = new SQLiteCommand(sql, sqlConn);
                command.ExecuteNonQuery();

                WriteCategoryToDb("Category:Key descriptions", "Key:", "{{KeyDescription");
                WriteCategoryToDb("Category:Tag descriptions", "Tag:", "{{ValueDescription");

                GenerateValidatorFile();

                stopWatch.Stop();

                Console.Write("Wiki pages gathered into temporary database, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);

                DisconnectTempDb();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error loading wiki pages into temporary database: " + "\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void ConnectTempDb()
        {
            try
            {
                stopWatch.Reset();
                stopWatch.Start();

                sqlConn.Open();

                sql = "CREATE TABLE IF NOT EXISTS wiki_pages (title VARCHAR(255), text VARCHAR(2000), onnode VARCHAR(10), onway VARCHAR(10), onarea VARCHAR(10), onrelation VARCHAR(10), status VARCHAR(50))";
                command = new SQLiteCommand(sql, sqlConn);
                command.ExecuteNonQuery();

                stopWatch.Stop();

                Console.Write("Connection estabilised to temporary database, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in connection setup: " + "\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        public void WriteCategoryToDb(string CategoryLabel, string LabelPrefix, string ValuePrefix)
        {
            try
            {
                stopWatch.Reset();
                stopWatch.Start();

                GetCategoryContents(CategoryLabel);

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

                foreach (string title in title_list.FindAll(f => f.Contains(LabelPrefix)))
                    GetPageContent(title);

                stopWatch.Stop();

                Console.Write("Page sources gathered, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error gathering page sources: " + "\n" + e.Message + "\n" + e.StackTrace);
            }

            transaction = sqlConn.BeginTransaction();

            foreach (Page single_page in pages)
            {
                // Getting wiki text, with some bugfix
                wiki_desc = single_page.value.ToString().Replace(@"""", "'").Replace("<nowiki>{yes", "<nowiki>[yes");

                if (wiki_desc.IndexOf(ValuePrefix) >= 0)
                {
                    start_pos = wiki_desc.IndexOf(ValuePrefix) + ValuePrefix.Length;
                    close_pos = ClosingBracket(wiki_desc, '{', '}', wiki_desc.IndexOf(ValuePrefix));

                    //for debug
                    //Console.WriteLine(wiki_desc);
                    //Console.WriteLine(start_pos.ToString() + ", " + close_pos.ToString());

                    _parameters = wiki_desc.Substring(start_pos, close_pos - start_pos + 1);
                    start_pos = _parameters.IndexOf('|');
                    parameters = _parameters.Substring(start_pos + 1).Split('|').ToList();

                    sql_col_list = "INSERT INTO wiki_pages (title, text";
                    sql_values = "values(\"" + single_page.title.ToString().Replace(' ', '_') + "\", \"" + wiki_desc + "\"";

                    foreach (var param in parameters)
                    {
                        if (param.IndexOf('=') > 0)
                        {
                            key = param.Substring(0, param.IndexOf('=')).Trim().ToLower();
                            value = param.Substring(param.IndexOf('=') + 1).Trim().ToLower();

                            // Value cleaning
                            value = value.Replace("}}", null).Replace("?", null);
                            if (value.IndexOf('<') >= 0)
                                value = value.Substring(0, value.IndexOf('<'));
                            if (value.IndexOf((char)13) >= 0)
                                value = value.Substring(0, value.IndexOf((char)13));
                            if (value.IndexOf((char)10) >= 0)
                                value = value.Substring(0, value.IndexOf((char)10));
                            if (key == "status")
                                value = value.Replace(" ", null);
                            value = value.Trim();

                            if (key == "onnode" || key == "onway" || key == "onarea" || key == "onrelation" || key == "status")
                            {
                                sql_col_list += ", " + key;
                                if (value.Length == 0)
                                    sql_values += ", NULL";
                                else
                                    sql_values += ", \"" + value + "\"";
                            }
                        }
                    }

                    sql_col_list += ") ";
                    sql_values += ")";

                    sql = sql_col_list + sql_values;

                    //for debug
                    //Console.WriteLine(sql);

                    command = new SQLiteCommand(sql, sqlConn);
                    command.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }

        public void GetCategoryContents(string CategoryLabel)
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
                        GetCategoryContents(element.Attribute("title").Value);
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

        public void GenerateValidatorFile()
        {
            string rn, mrn;

            using (StreamWriter file = new StreamWriter(@"wiki.validator.mapcss", false))
            {
                file.WriteLine("meta");
                file.WriteLine("{");
                file.WriteLine("    title: \"Generated Wiki Validations\";");
                file.WriteLine("    version: \"" + DateTime.Today.ToShortDateString() + "\";");
                file.WriteLine("    description: \"Checks for warnings based on Wiki articles\";");
                file.WriteLine("    author: \"Szabo Gabor\";");
                file.WriteLine("    watch - modified: true;");
                file.WriteLine("    link: \"https://github.com/AlteredCarrot71/josm-rule-generator\";");
                file.WriteLine("}");
                file.WriteLine();

                sql = "select title, rn, max(rn) over() as mrn from ( select substr(title, 5) as title, ROW_NUMBER() over() as rn from wiki_pages where title like 'Key:%' and status in ('approved', 'inuse', 'defacto') and onnode = 'no' and title not like '%network=%' and title not like '%ohm:%' and title not like '%3dr%' and title not like '%NPLG:UPRN%' and title not like '%placement=right_of:1%') order by rn";
                command = new SQLiteCommand(sql, sqlConn);
                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    rn = reader["rn"].ToString();
                    mrn = reader["mrn"].ToString();

                    if (rn == mrn)
                        file.WriteLine("node[" + reader["title"] + "] {");
                    else
                        file.WriteLine("node[" + reader["title"] + "],");
                }

                file.WriteLine("    throwWarning: tr(\"{0} on a node. Should be used on other type of object.\", \"{0.key}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");

                file.WriteLine();

                sql = "select title, rn, max(rn) over() as mrn from ( select substr(title, 5) as title, ROW_NUMBER() over() as rn from wiki_pages where title like 'Tag:%' and status in ('approved', 'inuse', 'defacto') and onnode = 'no' and title not like '%network=%' and title not like '%ohm:%' and title not like '%3dr%' and title not like '%NPLG:UPRN%' and title not like '%placement=right_of:1%') order by rn";
                command = new SQLiteCommand(sql, sqlConn);
                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    rn = reader["rn"].ToString();
                    mrn = reader["mrn"].ToString();

                    if (rn == mrn)
                        file.WriteLine("node[" + reader["title"] + "] {");
                    else
                        file.WriteLine("node[" + reader["title"] + "],");
                }

                file.WriteLine("    throwWarning: tr(\"{0} on a node. Should be used on other type of object.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");

                file.WriteLine();

                sql = "select title, rn, max(rn) over() as mrn from ( select substr(title, 5) as title, ROW_NUMBER() over() as rn from wiki_pages where title like 'Key:%' and status not in ('approved', 'inuse', 'defacto') and onnode = 'no' and title not like '%network=%' and title not like '%ohm:%' and title not like '%3dr%' and title not like '%NPLG:UPRN%' and title not like '%placement=right_of:1%' and title not like '%created_by%') order by rn";
                command = new SQLiteCommand(sql, sqlConn);
                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    rn = reader["rn"].ToString();
                    mrn = reader["mrn"].ToString();

                    if (rn == mrn)
                        file.WriteLine("node[" + reader["title"] + "] {");
                    else
                        file.WriteLine("node[" + reader["title"] + "],");
                }

                file.WriteLine("    throwWarning: tr(\"{0} is not used. Should use another tagging.\", \"{0.key}\");");
                file.WriteLine("    group: \"Tagging status\";");
                file.WriteLine("}");

                file.WriteLine();

                sql = "select title, rn, max(rn) over() as mrn from ( select substr(title, 5) as title, ROW_NUMBER() over() as rn from wiki_pages where title like 'Tag:%' and status not in ('approved', 'inuse', 'defacto') and onnode = 'no' and title not like '%network=%' and title not like '%ohm:%' and title not like '%3dr%' and title not like '%NPLG:UPRN%' and title not like '%placement=right_of:1%' and title not like '%created_by%') order by rn";
                command = new SQLiteCommand(sql, sqlConn);
                reader = command.ExecuteReader();

                while (reader.Read())
                {
                    rn = reader["rn"].ToString();
                    mrn = reader["mrn"].ToString();

                    if (rn == mrn)
                        file.WriteLine("node[" + reader["title"] + "] {");
                    else
                        file.WriteLine("node[" + reader["title"] + "],");
                }

                file.WriteLine("    throwWarning: tr(\"{0} is not used. Should use another tagging.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Tagging status\";");
                file.WriteLine("}");
            }
        }

        public void DisconnectTempDb()
        {
            try
            {
                stopWatch.Reset();
                stopWatch.Start();

                sqlConn.Close();

                stopWatch.Stop();

                Console.Write("Connection closed from temporary database, ");
                Console.WriteLine("elapsed {0} milliseconds.", stopWatch.ElapsedMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error in close of connection: " + "\n" + e.Message + "\n" + e.StackTrace);
            }
        }

        public static int ClosingBracket(string expression, char opening_bracket, char closing_bracket, int index)
        {
            int i;

            // If index given is invalid and is  
            // not an opening bracket.  
            if (expression[index] != opening_bracket)
            {
                return -1;
            }

            // Stack to store opening brackets.  
            Stack st = new Stack();

            // Traverse through string starting from  
            // given index.  
            for (i = index; i < expression.Length; i++)
            {

                // If current character is an  
                // opening bracket push it in stack.  
                if (expression[i] == opening_bracket)
                {
                    st.Push((int)expression[i]);
                } // If current character is a closing  
                  // bracket, pop from stack. If stack  
                  // is empty, then this closing  
                  // bracket is required bracket.  
                else if (expression[i] == closing_bracket)
                {
                    st.Pop();
                    if (st.Count == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }
    }
}
