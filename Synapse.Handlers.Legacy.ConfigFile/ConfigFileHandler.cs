using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.ConfigFile;

using Synapse.Core;

public class ConfigFileHandler : HandlerRuntimeBase
{
    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = new Workflow(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        wf.ExecuteAction(startInfo);

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters();

        wfp.Files = new List<FileType>();
        FileType file = new FileType();
        file.Type = ConfigType.KeyValue;
        file.RootDirectory = @"C:\Source";
        file.Source = @"MyApp\Development\java.properties.Development";
        file.Destination = @"MyApp\Development\java.properties";
        file.SettingsFile = new SettingFileType();
        file.SettingsFile.CreateIfNotFoundStr = "true";
        file.SettingsFile.HasEncryptedValuesStr = "false";
        file.SettingsFile.Value = @"MyApp\Development\Development.settings.csv";
        file.CopySourceStr = "true";

        wfp.Files.Add( file );

        String xml = wfp.Serialize( false );
        xml = xml.Substring( xml.IndexOf( "<" ) );
        return xml;
    }
}
