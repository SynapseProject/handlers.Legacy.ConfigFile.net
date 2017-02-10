using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace Synapse.Handlers.Legacy.ConfigFile
{
    public class PropertyFile
    {
        public enum Type
        {
            None,
            Java,
            Ini,
        }

        public Type FileType = Type.None;
        private List<PropertyFileLine> _lines = new List<PropertyFileLine>();
        private Dictionary<String, int> _index = new Dictionary<string, int>();
        private Dictionary<String, int> _sections = new Dictionary<string, int>();
        private String _currentSection = null;

        public PropertyFile() {}

        public PropertyFile(Type fileType)
        {
            this.FileType = fileType;
        }

        public PropertyFile(Type fileType, String file)
        {
            this.FileType = fileType;
            Load(file);

        }

        public void Load(String file)
        {
            String[] lines = File.ReadAllLines(file);
            foreach (String line in lines)
            {
                PropertyFileLine pfLine = null;
                if (this.FileType == Type.Java)
                    pfLine = ParseJava(line);
                else if (this.FileType == Type.Ini)
                    pfLine = ParseIni(line);

                if (pfLine != null)
                {
                    _lines.Add(pfLine);
                    int lineNumber = _lines.Count - 1;
                    if(pfLine.IsSection)
                    {
                        AddToSections(pfLine.Section, lineNumber);
                    }
                    else if (!(pfLine.IsNotAValidProperty))
                    {
                        AddToIndex(pfLine.Section, pfLine.Key, lineNumber);
                    }
                }
            }
        }

        public void Save(String file)
        {
            String[] outLines = new String[_lines.Count];
            for (int i=0; i<_lines.Count; i++)
                outLines[i] = _lines[i].RawLine;
            File.WriteAllLines(file, outLines);
        }

        public void SetProperty(String key, String value)
        {
            SetProperty(null, key, value);
        }

        public void SetProperty(String section, String key, String value)
        {
            PropertyFileLine line = GetProperty(section, key);
            if (line != null)
            {
                line.Value = value;
                line.UpdateRawLine();
            }
        }

        public void AddProperty(String key, String value)
        {
            AddProperty(null, key, value);
        }

        public void AddSection(String section)
        {
            if (!String.IsNullOrWhiteSpace(section) && (this.FileType == Type.Ini))
            {
                if (!_sections.ContainsKey(section))
                {
                    PropertyFileLine line = new PropertyFileLine();
                    int insertAt = GetNextLineIndex(section);
                    line.Section = section;
                    line.RawLine = "[" + section + "]";

                    _lines.Insert(insertAt, line);
                    _sections.Add(section, insertAt);
                    RebuildIndexes(insertAt);
                }
            }
        }

        public void AddProperty(String section, String key, String value)
        {
            AddSection(section);

            PropertyFileLine line = new PropertyFileLine();
            int insertAt = GetNextLineIndex(section);
            line.Section = section;
            line.Key = key;
            line.Value = value;
            line.Operator = @"=";
            line.UpdateRawLine();

            _lines.Insert(insertAt, line);
            AddToIndex(section, key, insertAt);
            RebuildIndexes(insertAt);
        }

        public Boolean Exists(String key)
        {
            return Exists(null, key);
        }

        public Boolean Exists(String section, String key)
        {
            String localKey = key;
            if (!String.IsNullOrWhiteSpace(section))
                localKey = section + "|" + key;
            return _index.ContainsKey(localKey);
        }

        private void RebuildIndexes(int lineNumberAdded)
        {
            List<String> keys = new List<String>(_index.Keys);
            foreach (String key in keys)
                if (_index[key] >= lineNumberAdded)
                    _index[key] = _index[key] + 1;

            keys = new List<string>(_sections.Keys);
            foreach (String key in keys)
                if (_sections[key] >= lineNumberAdded)
                    _sections[key] = _sections[key] + 1;
        }

        private int GetNextLineIndex(String section)
        {
            int insertAt = _lines.Count;

            if (String.IsNullOrWhiteSpace(section))
                insertAt = GetFirstSectionAfterLine(0);
            else if (_sections.ContainsKey(section))
                insertAt = GetFirstSectionAfterLine(_sections[section]);

            return insertAt;
        }

        private int GetFirstSectionAfterLine(int lineNumber)
        {
            int currentLowest = _lines.Count;
            foreach (KeyValuePair<String, int> entry in _sections)
            {
                if (entry.Value > lineNumber && entry.Value < currentLowest)
                    currentLowest = entry.Value;
            }
            return currentLowest;
        }
        
        private PropertyFileLine GetProperty(String key)
        {
            return GetProperty(null, key);
        }

        private PropertyFileLine GetProperty(String section, String key)
        {
            String localKey = key;
            if (!String.IsNullOrWhiteSpace(section))
                localKey = section + "|" + key;

            if (_index.ContainsKey(localKey))
            {
                int lineNumber = _index[localKey];
                return _lines[lineNumber];
            }

            return null;
        }

        private void AddToIndex(String section, String key, int lineNumber)
        {
            String localKey = key;
            if (!String.IsNullOrWhiteSpace(section))
                localKey = section + "|" + key;


            if (_index.ContainsKey(localKey))
            {
                int oldLine = _index[localKey];
                _lines[oldLine].IsDuplicate = true;
                _index[localKey] = lineNumber;
            }
            else
            {
                _index.Add(localKey, lineNumber);
            }
        }

        private void AddToSections(String section, int lineNumber)
        {
            if (_sections.ContainsKey(section))
            {
                int oldLine = _sections[section];
                _lines[oldLine].IsDuplicate = true;
                _sections[section] = lineNumber;
            }
            else
            {
                _sections.Add(section, lineNumber);
            }
        }

        private PropertyFileLine ParseJava(String line)
        {
            PropertyFileLine pfLine = new PropertyFileLine();
            pfLine.RawLine = line;

            if (String.IsNullOrWhiteSpace(line))
                pfLine.IsNotAValidProperty = true;
            else if (line.Trim().StartsWith("#") || line.Trim().StartsWith(";"))
            {
                pfLine.IsNotAValidProperty = true;
                pfLine.Comment = line;
            }
            else
            {
//                Regex getKeyValue = new Regex(@"^(\s*)(\S*?)(\s*[=|:]\s*)(.*)$");     // Relaxed Standards Around "No Spaces In Key" for Endur OpenLink Config Files
                Regex getKeyValue = new Regex(@"^(\s*)(.*?)(\s*[=|:]\s*)(.*)$");
                String matchLine = line.Replace(@"\=", @"\~");      // Replace Any Escaped Equal Signs So Regex Will Not Match Them
                matchLine = matchLine.Replace(@"\:", @"\`");        // Replace Any Escaped Colon Signs So Regex Will Not Match Them
                Match match = getKeyValue.Match(matchLine);
                if (match.Success)
                {
                    pfLine.LeadingSpace = match.Groups[1].Value;
                    pfLine.Key = match.Groups[2].Value;
                    pfLine.Operator = match.Groups[3].Value;
                    pfLine.Value = match.Groups[4].Value;

                    if (!String.IsNullOrWhiteSpace(pfLine.Key))
                    {
                        pfLine.Key = pfLine.Key.Replace(@"\~", @"\=");      // Replace Previously Escaped Equal Signs With Original Value
                        pfLine.Key = pfLine.Key.Replace(@"\`", @"\:");      // Replace Previously Escaped Equal Signs With Original Value
                    }

                    if (!String.IsNullOrWhiteSpace(pfLine.Value))
                    {
                        pfLine.Value = pfLine.Value.Replace(@"\~", @"\=");  // Replace Previously Escaped Equal Signs With Original Value
                        pfLine.Value = pfLine.Value.Replace(@"\`", @"\:");  // Replace Previously Escaped Equal Signs With Original Value
                    }
                } 
                else
                    pfLine.IsNotAValidProperty = true;
            }

            return pfLine;
        }

        private PropertyFileLine ParseIni(String line)
        {
            PropertyFileLine pfLine = new PropertyFileLine();
            pfLine.RawLine = line;

            if (String.IsNullOrWhiteSpace(line))
                pfLine.IsNotAValidProperty = true;
            else if (line.Trim().StartsWith(";") || line.Trim().StartsWith("#"))
            {
                pfLine.IsNotAValidProperty = true;
                pfLine.Comment = line;
            }
            else if (line.Trim().StartsWith("["))
            {
                Regex getSection = new Regex(@"^(\s*)\[(.*)\]\s*$");
                Match match = getSection.Match(line);
                pfLine.LeadingSpace = match.Groups[1].Value;
                pfLine.Section = match.Groups[2].Value;
                pfLine.IsSection = true;
                _currentSection = match.Groups[2].Value;
                
            }
            else
            {
                Regex getKeyValue = new Regex(@"^(\s*)(.*?)(\s*[=|:]\s*)(.*)$");
                String matchLine = line.Replace(@"\=", @"\~");      // Replace Any Escaped Equal Signs So Regex Will Not Match Them
                matchLine = matchLine.Replace(@"\:", @"\`");        // Replace Any Escaped Colon Signs So Regex Will Not Match Them
                Match match = getKeyValue.Match(matchLine);
                pfLine.LeadingSpace = match.Groups[1].Value;
                pfLine.Section = _currentSection;
                pfLine.Key = match.Groups[2].Value;
                pfLine.Operator = match.Groups[3].Value;
                pfLine.Value = match.Groups[4].Value;

                if (!String.IsNullOrWhiteSpace(pfLine.Key))
                {
                    pfLine.Key = pfLine.Key.Replace(@"\~", @"\=");      // Replace Previously Escaped Equal Signs With Original Value
                    pfLine.Key = pfLine.Key.Replace(@"\`", @"\:");      // Replace Previously Escaped Equal Signs With Original Value
                }

                if (!String.IsNullOrWhiteSpace(pfLine.Value))
                {
                    pfLine.Value = pfLine.Value.Replace(@"\~", @"\=");  // Replace Previously Escaped Equal Signs With Original Value
                    pfLine.Value = pfLine.Value.Replace(@"\`", @"\:");  // Replace Previously Escaped Equal Signs With Original Value
                }
            }

            return pfLine;
        }

    }

    public class PropertyFileLine
    {
        public String RawLine { get; set; }

        public Boolean IsNotAValidProperty { get; set; }
        public Boolean IsDuplicate { get; set; }
        public Boolean IsSection { get; set; }

        public String Section { get; set; }
        public String Key { get; set; }
        public String Value { get; set; }
        public String Operator { get; set; }
        public String Comment { get; set; }

        public String LeadingSpace { get; set; }

        public void UpdateRawLine()
        {
            this.RawLine = this.LeadingSpace + this.Key + this.Operator + this.Value;
        }
    }
}
