using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SampleToFlameGraph
{
	class MainClass
	{
		class Frame
		{
			[JsonIgnore]
			public int Depth;

			[JsonProperty("name")]
			public string Name { get; set; }

			[JsonProperty("value")]
			public int Value { get; set; }

			[JsonProperty("children")]
			public List<Frame> Children { get; } = new List<Frame>();
		}

		public static void Main(string[] args)
		{
			var fileName = args[0];
			var rx2 = new Regex(@"^([ +!:|]+)([0-9]+) (.*)", RegexOptions.Compiled);
			var stack = new Stack<Frame>();
			var listOfAllStacks = new List<Frame>();
			using (var sr = new StreamReader(fileName)) {
				string line;
				Frame currentFrame = new Frame();
				var rootFrame = currentFrame;
				var maxDepth = 0;
				while ((line = sr.ReadLine()) != null) {
					var match = rx2.Match(line);
					if (!match.Success)
						continue;
					var depth = match.Groups[1].Length;
					var count = int.Parse(match.Groups[2].Value);
					var txt = match.Groups[3].Value;
					if (txt.StartsWith(" ", StringComparison.Ordinal))
						continue;
					while (depth <= currentFrame.Depth) {
						currentFrame = stack.Pop();
					}
					if (stack.Count != ((depth - 4) / 2))
						throw new Exception();
					stack.Push(currentFrame);
					if (maxDepth < stack.Count)
						maxDepth = stack.Count;
					currentFrame.Value -= count;
					if (currentFrame.Value < 0 && currentFrame.Name != null)
						throw new Exception();
					currentFrame.Children.Add(currentFrame = new Frame() {
						Depth = depth,
						Value = count,
						Name = txt
					});
				}
				using (Stream stream = typeof(MainClass).Assembly.GetManifestResourceStream("SampleToFlameGraph.FlameGraphTemplate.html"))
				using (StreamReader reader = new StreamReader(stream)) {
					string result = reader.ReadToEnd();

					var dirName = Path.GetFileNameWithoutExtension(args[0]);
					Directory.CreateDirectory(dirName);

					var json = JsonConvert.SerializeObject(rootFrame, Formatting.None);
					string toWrite = result.Replace("$jsonPlaceholder$", json);
					toWrite = toWrite.Replace("window.screen.availHeight", (maxDepth * 18).ToString());
					File.WriteAllText(Path.Combine(dirName, "all.html"), toWrite);

					foreach (var frame in rootFrame.Children) {
						json = JsonConvert.SerializeObject(frame, Formatting.None);
						toWrite = result.Replace("$jsonPlaceholder$", json);
						toWrite = toWrite.Replace("window.screen.availHeight", (maxDepth * 18).ToString());
						File.WriteAllText(Path.Combine(dirName, frame.Name + ".html"), toWrite);
					}
				}
			}
		}
	}
}
