﻿using System;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using System.Threading;
using MonoDevelop.Core;
using System.IO;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Gui;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;

namespace PokeTheBeachball
{
	public class BeachballPoker
	{
		public readonly ConfigurationProperty<string> OutputPath = ConfigurationProperty.Create("BeachballPoker.OutputPath", System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Desktop"));
		public static BeachballPoker Instance { get; } = new BeachballPoker();

		BeachballPoker() { }

		Socket socket;
		Thread tcpLoopThread;
		Thread dumpsReaderThread;
		Thread pumpErrorThread;
		TcpListener listener;
		Process process;

		void tcpLoop()
		{
			byte[] buffer = new byte[1];
			ManualResetEvent waitUIThread = new ManualResetEvent(false);
			var sw = Stopwatch.StartNew();
			while (true) {
				sw.Restart();
				var readBytes = socket.Receive(buffer, 1, SocketFlags.None);
				if (readBytes != 1)
					return;
				waitUIThread.Reset();
				Runtime.RunInMainThread(delegate {
					waitUIThread.Set();
				});
				waitUIThread.WaitOne();
				socket.Send(buffer);
			}
		}

		public bool IsListening { get; private set; }

		public void Start()
		{
			if (IsListening)
				return;
			IsListening = true;
			listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			listener.AcceptSocketAsync().ContinueWith(t => {
				if (!t.IsFaulted && !t.IsCanceled) {
					socket = t.Result;
					tcpLoopThread = new Thread(new ThreadStart(tcpLoop));
					tcpLoopThread.IsBackground = true;
					tcpLoopThread.Start();
					listener.Stop();
				}
			});
			var port = ((IPEndPoint)listener.LocalEndpoint).Port;
			process = new Process();
			process.StartInfo.FileName = "mono";
			process.StartInfo.Arguments = $"PokeTheBeachballDaemon.exe {port} {Process.GetCurrentProcess().Id}";
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.RedirectStandardOutput = true;
			process.StartInfo.RedirectStandardError = true;//Ignore it, otherwise it goes to IDE logging
			process.Start();
			process.StandardError.ReadLine();
			dumpsReaderThread = new Thread(new ThreadStart(dumpsReader));
			dumpsReaderThread.IsBackground = true;
			dumpsReaderThread.Start();

			pumpErrorThread = new Thread(new ThreadStart(pumpErrorStream));//We need to read this...
			pumpErrorThread.IsBackground = true;
			pumpErrorThread.Start();
		}

		[DllImport("__Internal")]
		extern static string mono_pmip(long offset);
		Dictionary<long, string> methodsCache = new Dictionary<long, string>();

		void pumpErrorStream()
		{
			while (!(process?.HasExited ?? true)) {
				process?.StandardError?.ReadLine();
			}
		}

		void dumpsReader()
		{
			var rx = new Regex(@"\?\?\?  \(in <unknown binary>\)  \[0x([0-9a-f]+)\]", RegexOptions.Compiled);
			while (!(process?.HasExited ?? true)) {
				var fileName = process.StandardOutput.ReadLine();
				if (File.Exists(fileName) && new FileInfo(fileName).Length > 0) {
					var outputFilename = Path.Combine(OutputPath, BrandingService.ApplicationName + "_Profiling_" + DateTime.Now.ToString("s") + ".txt");
					using (var sr = new StreamReader(fileName))
					using (var sw = new StreamWriter(outputFilename)) {
						string line;
						while ((line = sr.ReadLine()) != null) {
							if (rx.IsMatch(line)) {
								var match = rx.Match(line);
								var offset = long.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
								string pmipMethodName;
								if (!methodsCache.TryGetValue(offset, out pmipMethodName)) {
									pmipMethodName = mono_pmip(offset)?.TrimStart();
									methodsCache.Add(offset, pmipMethodName);
								}
								if (pmipMethodName != null) {
									line = line.Remove(match.Index, match.Length);
									line = line.Insert(match.Index, pmipMethodName);
								}
							}
							sw.WriteLine(line);
						}
					}
				}
			}
		}

		public void Stop()
		{
			if (!IsListening)
				return;
			IsListening = false;
			listener.Stop();
			listener = null;
			process.Kill();
			process = null;
		}
	}
}

