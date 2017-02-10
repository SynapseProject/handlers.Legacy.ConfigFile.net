using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Xml;
using System.Text;

using Alphaleonis.Win32.Filesystem;

namespace Synapse.Handlers.Legacy.ConfigFile
{
	[Serializable, XmlRoot( "ConfigFile" )]
	public class WorkflowParameters
	{
        private List<FileType> _files = new List<FileType>();
        [XmlIgnore]
        public Boolean _runSequential = false;

        #region Public Global Workflow Parameters

        [XmlArrayItem(ElementName = "File")]
        public List<FileType> Files
        {
            get { return _files; }
            set { _files = value; }
        }


        [XmlElement("RunSequential")]
        public String RunSequentialStr { get; set; }

        #endregion
        #region Validation Flags

        [XmlIgnore]
        public bool IsValidSourceFiles { get; protected set; }
        [XmlIgnore]
        public bool IsValidDestinations { get; protected set; }
        [XmlIgnore]
        public bool IsValidSettingsFiles { get; protected set; }
        [XmlIgnore]
        public bool HasSettings { get; protected set; }
        [XmlIgnore]
		public bool IsValid { get; protected set; }
        
        #endregion
        #region Public Workflow Parameter Methods

		public virtual String[] PrepareAndValidate() 
        {
            List<String> errors = new List<String>();
            IsValidSourceFiles = true;
            IsValidDestinations = true;
            IsValidSettingsFiles = true;
            HasSettings = true;
            IsValid = true;

            foreach (FileType file in Files)
            {
                file.Parse();
                if (!File.Exists(file.Source))
                {
                    IsValidSourceFiles = false;
                    errors.Add("Source File [" + file.Source + "] Not Found.");
                }

                if (file.SettingsFile == null)
                {
                    if (file.Type == ConfigType.XmlTransform)
                    {
                        IsValidSettingsFiles = false;
                        errors.Add("Settings File Required For Type [" + file.Type + "]");
                    }
                }
                else if (!String.IsNullOrWhiteSpace(file.SettingsFile.Value) || file.Type == ConfigType.XmlTransform)
                {
                    if (!File.Exists(file.SettingsFile.Value))
                    {
                        IsValidSettingsFiles = false;
                        errors.Add("Settings File [" + file.SettingsFile.Value + "] Not Found.");
                    }
                }

                if (file.SettingsFile == null && (file.Settings == null || file.Settings.Count == 0))
                {
                    HasSettings = false;
                    errors.Add("No Replacement Settings Found In Package.");
                }

                if (!String.IsNullOrWhiteSpace(file.Destination))
                {
                    if (!(Directory.Exists(Path.GetDirectoryName(file.Destination))))
                    {
                        IsValidDestinations = false;
                        errors.Add("Destination Directory [" + Path.GetDirectoryName(file.Destination) + "] Not Found.");
                    }
                }
            }



            IsValid = IsValidSourceFiles && IsValidSettingsFiles && IsValidDestinations && HasSettings;

            return errors.ToArray();

        }

		public virtual void Serialize(string filePath)
		{
			Utils.Serialize<WorkflowParameters>( this, true, filePath );
		}

        public virtual String Serialize(bool indented = true)
        {
            return Utils.Serialize<WorkflowParameters>(this, indented);
        }

        public static WorkflowParameters Deserialize(XmlElement el)
        {
            XmlSerializer s = new XmlSerializer(typeof(WorkflowParameters));
            return (WorkflowParameters)s.Deserialize(new System.IO.StringReader(el.OuterXml));
        }
        
        public static WorkflowParameters Deserialize(string filePath)
		{
			return Utils.DeserializeFile<WorkflowParameters>( filePath );
		}

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(">> RunSequentially   : " + this._runSequential);
            foreach (FileType file in Files)
                sb.Append(file.ToString());
            return sb.ToString();
        }

        public void Parse()
        {
            _runSequential = (RunSequentialStr != null);
            try { _runSequential = Boolean.Parse(RunSequentialStr) != false; }
            catch (Exception) { }

        }

        #endregion
    }

    public class FileType
    {
        private List<SettingType> _settings = new List<SettingType>();

        public ConfigType Type { get; set; }
        public String RootDirectory { get; set; }
        public String Source { get; set; }
        public String Destination { get; set; }
        public SettingFileType SettingsFile { get; set; }
        [XmlElement("CopySource")]
        public String CopySourceStr { get; set; }

        [XmlIgnore]
        public Boolean _CopySource = false;

        public void Parse()
        {
            try { _CopySource = Boolean.Parse(CopySourceStr); }
            catch (Exception) { }

            if (!String.IsNullOrWhiteSpace(RootDirectory))
            {
                // If Path.Combine Fails, Just Use Original Value
                try { Source = Path.Combine(RootDirectory, Source); } catch (Exception) { }
                try { Destination = Path.Combine(RootDirectory, Destination); } catch (Exception) { }
                try { SettingsFile.Value = Path.Combine(RootDirectory, SettingsFile.Value); } catch (Exception) { }
            }

            // If No Destination File Specified, Just Overwrite The Source File.
            if (String.IsNullOrWhiteSpace(Destination))
                Destination = Source;
        }
        
        [XmlArrayItem(ElementName = "Setting")]
        public List<SettingType> Settings
        {
            get { return _settings; }
            set { _settings = value; }
        }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(">> File");
            sb.AppendLine("   >> Type                  : " + this.Type);
            sb.AppendLine("   >> RootDirectory         : " + this.RootDirectory);
            sb.AppendLine("   >> Source                : " + this.Source);
            sb.AppendLine("   >> Destination           : " + this.Destination);
            sb.AppendLine("   >> SettingsFile          : " + (this.SettingsFile == null ? "" : this.SettingsFile.Value));
            sb.AppendLine("   >> CopySource            : " + this._CopySource);
            foreach (SettingType setting in Settings)
            {
                if (setting != null)
                    sb.Append(setting.ToString());
            }

            return sb.ToString();
        }

    }

    public class SettingType
    {
        public String Section { get; set; }
        public String Key { get; set; }
        public ValueType Value { get; set; }
        [XmlAttribute("CreateIfNotFound")]
        public String CreateIfNotFoundStr { get; set; }

        [XmlIgnore]
        public Boolean _CreateIfNotFound = false;

        public void Parse()
        {
            try { _CreateIfNotFound = Boolean.Parse(CreateIfNotFoundStr); }
            catch (Exception) { }
        }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("   >> Setting");
            sb.AppendLine("      >> Section            : " + this.Section);
            sb.AppendLine("      >> Key                : " + this.Key);
            if (this.Value != null)
            {
                sb.AppendLine("      >> Value              : " + this.Value.Value);
                sb.AppendLine("         >> IsEncrypted     : " + this.Value.IsEncryptedStr);
            }
            sb.AppendLine("      >> CreateIfNotFound   : " + this._CreateIfNotFound);

            return sb.ToString();
        }
    }

    public class ValueType
    {
        [XmlAttribute("IsEncrypted")]
        public String IsEncryptedStr { get; set; }
        [XmlText]
        public String Value { get; set; }

        [XmlIgnore]
        public Boolean _IsEncrypted = false;

        public void Parse()
        {
            try { _IsEncrypted = Boolean.Parse(IsEncryptedStr); }
            catch (Exception) { }
        }

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();
            return sb.ToString();
        }
    }

    public class SettingFileType
    {
        [XmlAttribute("CreateIfNotFound")]
        public String CreateIfNotFoundStr { get; set; }
        [XmlAttribute("HasEncryptedValues")]
        public String HasEncryptedValuesStr { get; set; }
        [XmlText]
        public String Value { get; set; }

        [XmlIgnore]
        public Boolean _CreateIfNotFound = false;
        [XmlIgnore]
        public Boolean _HasEncryptedValues = false;

        public void Parse()
        {
            try { _CreateIfNotFound = Boolean.Parse(CreateIfNotFoundStr); }
            catch (Exception) { }
            try { _HasEncryptedValues = Boolean.Parse(HasEncryptedValuesStr); }
            catch (Exception) { }
        }
    }

    
}