using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;

using Alphaleonis.Win32.Filesystem;
using Synapse.Core;

using System.Security.Cryptography.Utility;

using config = Synapse.Handlers.Legacy.ConfigFile.Properties.Settings;

namespace Synapse.Handlers.Legacy.ConfigFile
{
	public class Workflow
	{
		protected WorkflowParameters _wfp = null;

        public Action<string, string, LogLevel, Exception> OnLogMessage;
        public Func<string, string, StatusType, long, int, bool, Exception, bool> OnProgress;

        public Workflow(WorkflowParameters wfp)
		{
			_wfp = wfp;
		}


		public WorkflowParameters Parameters { get { return _wfp; } set { _wfp = value as WorkflowParameters; } }

		public void ExecuteAction()
		{
			string context = "ExecuteAction";

			string msg = Utils.GetHeaderMessage( string.Format( "Entering Main Workflow."));
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress(context, _wfp.Serialize(false));
            Stopwatch clock = new Stopwatch();
            clock.Start();

            Exception ex = null;
            try
            {
                bool isValid = ValidateParameters();

                if (isValid)
                {
                    RunMainWorkflow();
                }
                else
                {
                    OnStepProgress(context, "Package Validation Failed");
                    throw new Exception("Package Validation Failed");
                }
            }
            catch (Exception exception)
            {
                ex = exception;
            }

            bool ok = ex == null;
            msg = Utils.GetHeaderMessage(string.Format("End Main Workflow: {0}, Total Execution Time: {1}",
                ok ? "Complete." : "One or more steps failed.", clock.ElapsedSeconds()));
            OnProgress(context, msg, ok ? StatusType.Complete : StatusType.Failed, 0, int.MaxValue, false, ex);

        }

        public virtual void RunMainWorkflow()
        {
            try
            {
                OnStepProgress("RunMainWorkflow", "Starting Main Workflow");

                _wfp.Parse();
                if (_wfp._runSequential == true || _wfp.Files.Count <= 1)
                {
                    OnStepProgress("RunMainWorkflow", "Processing Files Sequentially.");
                    foreach (FileType file in _wfp.Files)
                        MungeFile(file);
                }
                else
                {
                    OnStepProgress("RunMainWorkflow", "Processing Files In Parallel.");
                    Parallel.ForEach(_wfp.Files, file => MungeFile(file));
                }
            } catch (Exception e)
            {
                OnStepProgress("ERROR", e.Message);
                OnStepFinished("ERROR", e.StackTrace);
                throw e;
            }

        }

        public void MungeFile(FileType file)
        {
            // Parse Boolean Values
            file.Parse();
            if (file.SettingsFile != null)
                file.SettingsFile.Parse();
            foreach (SettingType setting in file.Settings)
            {
                if (setting != null)
                {
                    setting.Parse();
                    if (setting.Value != null)
                        setting.Value.Parse();
                }
            }
            if (file._CopySource == true) {
                OnStepProgress("MungeFile", "Backing Up Source File To [" + file.Source + ".orig]");
                File.Copy(file.Source, file.Source + ".orig", true);
            }

            switch (file.Type)
            {
                case ConfigType.XmlTransform:
                    OnStepProgress("MungeFile", "Starting XmlTransform From [" + file.Source + "] To [" + file.Destination + "]");
                    Munger.XMLTransform(file.Source, file.Destination, file.SettingsFile.Value);
                    OnStepProgress("MungeFile", "Finished XmlTransform From [" + file.Source + "] To [" + file.Destination + "]");
                    break;
                case ConfigType.KeyValue:
                    OnStepProgress("MungeFile", "Starting KeyValue Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    Munger.KeyValue(PropertyFile.Type.Java, file.Source, file.Destination, file.SettingsFile, file.Settings);
                    OnStepProgress("MungeFile", "Finished KeyValue Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    break;
                case ConfigType.INI:
                    OnStepProgress("MungeFile", "Starting INI File Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    Munger.KeyValue(PropertyFile.Type.Ini, file.Source, file.Destination, file.SettingsFile, file.Settings);
                    OnStepProgress("MungeFile", "Finished INI File Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    break;
                case ConfigType.XPath:
                    OnStepProgress("MungeFile", "Starting XPath Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    Munger.XPath(file.Source, file.Destination, file.SettingsFile, file.Settings);
                    OnStepProgress("MungeFile", "Finished XPath Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    break;
                case ConfigType.Regex:
                    OnStepProgress("MungeFile", "Starting Regex Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    Munger.RegexMatch(file.Source, file.Destination, file.SettingsFile, file.Settings);
                    OnStepProgress("MungeFile", "Finished Regex Replacement From [" + file.Source + "] To [" + file.Destination + "]");
                    break;
                default:
                    OnStepProgress("RunMainWorkflow", "Unsupported ConfigFile Type [" + file.Type.ToString() + "].");
                    break;
            }
        }

        bool ValidateParameters()
        {
            string context = "Validate";
            const int padding = 50;

            OnStepProgress(context, Utils.GetHeaderMessage("Begin [PrepareAndValidate]"));

            String[] errors = _wfp.PrepareAndValidate();

            if (_wfp.IsValid == false)
                foreach (String error in errors)
                    OnStepProgress(context, error);

            OnStepProgress(context, Utils.GetMessagePadRight("IsValidSourceFiles", _wfp.IsValidSourceFiles, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("IsValidDestinations", _wfp.IsValidDestinations, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("IsValidSettingsFiles", _wfp.IsValidSettingsFiles, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("HasSettings", _wfp.HasSettings, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("IsValid", _wfp.IsValid, padding));
            OnStepProgress(context, Utils.GetHeaderMessage("End [PrepareAndValidate]"));

            return _wfp.IsValid;
        }


        #region NotifyProgress Events
		int _cheapSequence = 0;

		void p_StepProgress(object sender, AdapterProgressEventArgs e)
		{
            OnProgress(e.Context, e.Message, StatusType.Running, 0, _cheapSequence, false, e.Exception);
		}

		/// <summary>
		/// Notify of step beginning. If return value is True, then cancel operation.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		bool OnStepStarting(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
            return false;
        }

        /// <summary>
        /// Notify of step progress.
        /// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
        /// </summary>
        /// <param name="context">The method name.</param>
        /// <param name="message">Descriptive message.</param>
        protected void OnStepProgress(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
		}

		/// <summary>
		/// Notify of step completion.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		protected void OnStepFinished(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
		}
		#endregion

	}

}