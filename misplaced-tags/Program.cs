using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace misplaced_tags
{
    class Program
    {
        public Stopwatch stopWatch = new();

        public List<Page> pages = new();
        public List<PageData> pagedatas = new();

        public int start_pos;
        public int close_pos;

        public string wiki_desc;
        public string _parameters;

        public List<String> parameters;

        public string key;
        public string value;

        public List<string> validStatus = new() { "approved", "inuse", "defacto" };

        static void Main()
        {
            var instance = new Program();

            instance.GenerateValidatorFile();
        }

        public void GenerateValidatorFile()
        {
            // Load XML data
            XmlSerializer serializer = new(typeof(List<Page>));
            FileStream fs = new("pages.xml", FileMode.Open);
            pages = (List<Page>)serializer.Deserialize(fs);

            foreach (Page single_page in pages)
            {
                PageData _pageData = new();

                // Getting wiki text, with some bugfix
                wiki_desc = single_page.Value.ToString().Replace(@"""", "'").Replace("<nowiki>{yes", "<nowiki>[yes");

                if (wiki_desc.Contains("{{KeyDescription", StringComparison.CurrentCulture))
                {
                    start_pos = wiki_desc.IndexOf("{{KeyDescription") + "{{KeyDescription".Length;
                    close_pos = ClosingBracket(wiki_desc, '{', '}', wiki_desc.IndexOf("{{KeyDescription"));

                    _parameters = wiki_desc.Substring(start_pos, close_pos - start_pos + 1);
                    start_pos = _parameters.IndexOf('|');
                    parameters = _parameters[(start_pos + 1)..].Split('|').ToList();

                    _pageData.Title = single_page.Title.ToString().Replace(' ', '_');

                    foreach (var param in parameters)
                    {
                        if (param.IndexOf('=') > 0)
                        {
                            key = param[..param.IndexOf('=')].Trim().ToLower();
                            value = param[(param.IndexOf('=') + 1)..].Trim().ToLower();

                            // Value cleaning
                            value = value.Replace("}}", null).Replace("?", null);
                            if (value.Contains('<'))
                                value = value[..value.IndexOf('<')];
                            if (value.Contains((char)13))
                                value = value[..value.IndexOf((char)13)];
                            if (value.Contains((char)10))
                                value = value[..value.IndexOf((char)10)];
                            if (key == "status")
                                value = value.Replace(" ", null);
                            value = value.Trim();

                            if (value.Length != 0)
                                if (key == "key")
                                    _pageData.Key = value;
                                else if (key == "value")
                                    _pageData.Value = value;
                                else if (key == "onnode")
                                    _pageData.OnNode = value;
                                else if (key == "onway")
                                    _pageData.OnWay = value;
                                else if (key == "onarea")
                                    _pageData.OnArea = value;
                                else if (key == "onrelation")
                                    _pageData.OnRelation = value;
                                else if (key == "status")
                                    _pageData.Status = value;
                        }
                    }

                    pagedatas.Add(_pageData);
                }

                if (wiki_desc.Contains("{{ValueDescription", StringComparison.CurrentCulture))
                {
                    start_pos = wiki_desc.IndexOf("{{ValueDescription") + "{{ValueDescription".Length;
                    close_pos = ClosingBracket(wiki_desc, '{', '}', wiki_desc.IndexOf("{{ValueDescription"));

                    _parameters = wiki_desc.Substring(start_pos, close_pos - start_pos + 1);
                    start_pos = _parameters.IndexOf('|');
                    parameters = _parameters[(start_pos + 1)..].Split('|').ToList();

                    _pageData.Title = single_page.Title.ToString().Replace(' ', '_');

                    foreach (var param in parameters)
                    {
                        if (param.IndexOf('=') > 0)
                        {
                            key = param[..param.IndexOf('=')].Trim().ToLower();
                            value = param[(param.IndexOf('=') + 1)..].Trim().ToLower();

                            // Value cleaning
                            value = value.Replace("}}", null).Replace("?", null);
                            if (value.Contains('<'))
                                value = value[..value.IndexOf('<')];
                            if (value.Contains((char)13))
                                value = value[..value.IndexOf((char)13)];
                            if (value.Contains((char)10))
                                value = value[..value.IndexOf((char)10)];
                            if (key == "status")
                                value = value.Replace(" ", null);
                            value = value.Trim();

                            if (value.Length != 0)
                                if (key == "key")
                                    _pageData.Key = value;
                                else if (key == "value")
                                    _pageData.Value = value;
                                else if (key == "onnode")
                                    _pageData.OnNode = value;
                                else if (key == "onway")
                                    _pageData.OnWay = value;
                                else if (key == "onarea")
                                    _pageData.OnArea = value;
                                else if (key == "onrelation")
                                    _pageData.OnRelation = value;
                                else if (key == "status")
                                    _pageData.Status = value;
                        }
                    }

                    pagedatas.Add(_pageData);
                }
            }

            pagedatas.RemoveAll(f => !validStatus.Contains(f.Status));

            using StreamWriter file = new(@"wiki.misplaced-tags.validator.mapcss", false);
            file.WriteLine("meta");
            file.WriteLine("{");
            file.WriteLine("    title: \"Generated Wiki validations for misplaced tags\";");
            file.WriteLine("    version: \"" + DateTime.Today.ToShortDateString() + "\";");
            file.WriteLine("    description: \"Checks for warnings based on Wiki articles\";");
            file.WriteLine("    author: \"Szabo Gabor\";");
            file.WriteLine("    watch - modified: true;");
            file.WriteLine("    link: \"https://github.com/AlteredCarrot71/josm-rule-generator\";");
            file.WriteLine("}");
            file.WriteLine();

            #region Found on node ...
            if (pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "no").SkipLast(1))
                    file.WriteLine("node[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "no").TakeLast(1))
                    file.WriteLine("node[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a node. Should be used on a way.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes").SkipLast(1))
                    file.WriteLine("node[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes").TakeLast(1))
                    file.WriteLine("node[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a node. Should be used on a relation.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "yes").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "yes").SkipLast(1))
                    file.WriteLine("node[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "yes").TakeLast(1))
                    file.WriteLine("node[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a node. Should be used on a way or relation.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }
            #endregion

            #region Found on way ...
            if (pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no" && f.OnArea == "yes").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no" && f.OnArea == "yes").SkipLast(1))
                    file.WriteLine("way!:closed[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no" && f.OnArea == "yes").TakeLast(1))
                    file.WriteLine("way!:closed[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a way. Should be used on a node.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no" && f.OnArea == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no" && f.OnArea == "no").SkipLast(1))
                    file.WriteLine("way[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no" && f.OnArea == "no").TakeLast(1))
                    file.WriteLine("way[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a way. Should be used on a node.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "yes").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "yes").SkipLast(1))
                    file.WriteLine("way!:closed[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "yes").TakeLast(1))
                    file.WriteLine("way!:closed[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a way. Should be used on a relation.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "no").SkipLast(1))
                    file.WriteLine("way[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "no").TakeLast(1))
                    file.WriteLine("way[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a way. Should be used on a relation.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "yes").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "yes").SkipLast(1))
                    file.WriteLine("way!:closed[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "yes").TakeLast(1))
                    file.WriteLine("way!:closed[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a way. Should be used on a node or relation.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "no").SkipLast(1))
                    file.WriteLine("way[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "yes" && f.OnArea == "no").TakeLast(1))
                    file.WriteLine("way[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a way. Should be used on a node or relation.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }
            #endregion

            #region Found on relation ...
            if (pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "no").SkipLast(1))
                    file.WriteLine("relation[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "no" && f.OnWay == "yes" && f.OnRelation == "no").TakeLast(1))
                    file.WriteLine("relation[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a relation. Should be used on a way.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no").SkipLast(1))
                    file.WriteLine("relation[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "no" && f.OnRelation == "no").TakeLast(1))
                    file.WriteLine("relation[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a relation. Should be used on a node.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }

            if (pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "yes" && f.OnRelation == "no").Any())
            {
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "yes" && f.OnRelation == "no").SkipLast(1))
                    file.WriteLine("relation[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "],");
                foreach (PageData record in pagedatas.Where(f => f.OnNode == "yes" && f.OnWay == "yes" && f.OnRelation == "no").TakeLast(1))
                    file.WriteLine("relation[\"" + record.Key + "\"" + (string.IsNullOrEmpty(record.Value) ? "" : ("=\"" + record.Value + "\"")) + "] {");

                file.WriteLine("    throwWarning: tr(\"{0} on a relation. Should be used on a node or way.\", \"{0.tag}\");");
                file.WriteLine("    group: \"Misplaced tagging\";");
                file.WriteLine("}");
                file.WriteLine();
            }
            #endregion
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
            Stack st = new();

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
