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
}
