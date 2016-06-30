using System;
using MonoDevelop.Components.Commands;
namespace PokeTheBeachball
{
	public class StartStopListeningHandler : CommandHandler
	{
		protected override void Run()
		{
			if (BeachballPoker.Instance.IsListening)
				BeachballPoker.Instance.Stop();
			else
				BeachballPoker.Instance.Start();
		}

		protected override void Update(CommandInfo info)
		{
			info.Text = BeachballPoker.Instance.IsListening ? "Stop poking Beachball" : "Start poking Beachball";
			base.Update(info);
		}
	}
}

