using Sitecore;
using Sitecore.Data.Items;
using Sitecore.DataExchange;
using Sitecore.DataExchange.Local.Commands;
using Sitecore.DataExchange.Local.Converters;
using Sitecore.DataExchange.Local.Events.PipelineBatchRunnerEvents;
using Sitecore.DataExchange.Local.Runners;
using Sitecore.DataExchange.Models;
using Sitecore.DataExchange.Runners;
using Sitecore.Eventing;
using Sitecore.Globalization;
using Sitecore.Jobs;
using Sitecore.Shell.Framework.Commands;
using Sitecore.Web;
using Sitecore.Web.UI.Sheer;
using System;


namespace Sitecore.Support.DataExchange.Local.Commands
{
  [Serializable]
  public class StopPipelineBatchCommand : BaseContextCommand
  {
    private bool _isPipelineBatchConverterSet;
    private bool _isRunnerSet;
    private IConverter<Item, PipelineBatch> _pipelineBatchConverter;
    private static IPipelineBatchRunner<Job> _runner;
    private const int DefaultRefreshTimer = 2;
    private PipelineBatch pipelineBatch;

    public StopPipelineBatchCommand()
    {
      base.SupportedTemplateIds.Add(Guid.Parse("{075C4FBD-F54E-4E6D-BD54-D49BDA0913D8}"));
    }

    protected void DialogProcessor(ClientPipelineArgs args)
    {
      if (args.IsPostBack)
      {
        if (args.Result == "yes")
        {
          this.PipelineBatchRunner.Stop(this.pipelineBatch);
          object[] objArray1 = new object[] { this.pipelineBatch.Name };
          Context.ClientPage.ClientResponse.Alert(Translate.Text("{0} is being stopped.", objArray1));
          Context.ClientPage.ClientResponse.Timer(string.Format("item:load(id={0})", args.Parameters["ItemID"]), 2);
        }
      }
      else
      {
        object[] objArray2 = new object[] { this.pipelineBatch.Name };
        Context.ClientPage.ClientResponse.Confirm(Translate.Text("Are you sure you want to stop {0}?", objArray2));
        args.WaitForPostBack();
      }
    }

    public override void Execute(CommandContext context)
    {
      if (((this.PipelineBatchRunner != null) && (context != null)) && ((context.Items != null) && (context.Items.Length != 0)))
      {
        Item source = context.Items[0];
        IConverter<Item, PipelineBatch> pipelineBatchConverter = this.PipelineBatchConverter;
        if (pipelineBatchConverter != null)
        {
          this.pipelineBatch = pipelineBatchConverter.Convert(source);
          if ((this.pipelineBatch != null) && this.PipelineBatchRunner.IsRunning(this.pipelineBatch))
          {
            int result = 0;
            if (!int.TryParse(context.Parameters["refreshTimer"], out result))
            {
              result = 2;
            }
            if ((((InProcessPipelineBatchRunner)this.PipelineBatchRunner) != null) && ((InProcessPipelineBatchRunner)this.PipelineBatchRunner).IsRunningRemotely(this.pipelineBatch))
            {
              string serverName = ((InProcessPipelineBatchRunner)this.PipelineBatchRunner).GetServerName(this.pipelineBatch);
              object[] objArray1 = new object[] { context.Items[0].Name, serverName };
              Context.ClientPage.ClientResponse.Alert(Translate.Text("The {0} is running on {1} server and cannot be stopped on this server.", objArray1));
              Context.ClientPage.ClientResponse.Timer(string.Format("item:load(id={0})", context.Items[0].ID), result);
            }
            else
            {
              ClientPipelineArgs args = new ClientPipelineArgs();
              args.Parameters["ItemID"] = context.Items[0].ID.ToString();
              Context.ClientPage.Start(this, "DialogProcessor", args);
            }
          }
        }
      }
    }

    private void PipelineBatchRunnerOnFinished(object sender, PipelineBatchRunnerEventArgs pipelineBatchRunnerEventArgs)
    {
      PipelineBatchRunnerEventRemoteFinished finished1 = new PipelineBatchRunnerEventRemoteFinished
      {
        Id = pipelineBatchRunnerEventArgs.PipelineBatch.ID
      };
      EventManager.QueueEvent<PipelineBatchRunnerEventRemoteFinished>(finished1);
    }

    private void PipelineBatchRunnerOnStarted(object sender, PipelineBatchRunnerEventArgs pipelineBatchRunnerEventArgs)
    {
      PipelineBatchRunnerEventRemoteStarted started1 = new PipelineBatchRunnerEventRemoteStarted
      {
        Id = pipelineBatchRunnerEventArgs.PipelineBatch.ID,
        ServerName = WebUtil.GetHostName()
      };
      EventManager.QueueEvent<PipelineBatchRunnerEventRemoteStarted>(started1);
    }

    protected override bool ShouldEnableButton(Item contextItem)
    {
      if (base.ShouldEnableButton(contextItem))
      {
        IPipelineBatchRunner<Job> pipelineBatchRunner = this.PipelineBatchRunner;
        if ((pipelineBatchRunner != null) && (this.PipelineBatchConverter != null))
        {
          this.pipelineBatch = this.PipelineBatchConverter.Convert(contextItem);
          if (((this.pipelineBatch != null) && this.pipelineBatch.Enabled) && pipelineBatchRunner.IsRunning(this.pipelineBatch))
          {
            return true;
          }
        }
      }
      return false;
    }

    public IConverter<Item, PipelineBatch> PipelineBatchConverter
    {
      get
      {
        if ((this._pipelineBatchConverter == null) && !this._isPipelineBatchConverterSet)
        {
          this._pipelineBatchConverter = new ItemToPipelineBatchConverter();
        }
        return this._pipelineBatchConverter;
      }
      set
      {
        this._isPipelineBatchConverterSet = true;
        this._pipelineBatchConverter = value;
      }
    }

    public IPipelineBatchRunner<Job> PipelineBatchRunner
    {
      get
      {
        if (!this._isRunnerSet && (_runner == null))
        {
          _runner = new InProcessPipelineBatchRunner();
          ((InProcessPipelineBatchRunner)_runner).SubscribeRemoteEvents();
          _runner.Started += new EventHandler<PipelineBatchRunnerEventArgs>(this.PipelineBatchRunnerOnStarted);
          _runner.Finished += new EventHandler<PipelineBatchRunnerEventArgs>(this.PipelineBatchRunnerOnFinished);
        }
        return _runner;
      }
      set
      {
        this._isRunnerSet = true;
        _runner = value;
      }
    }
  }
}
