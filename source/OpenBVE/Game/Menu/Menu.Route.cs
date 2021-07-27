﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Text;
using LibRender2.Primitives;
using OpenBveApi;
using OpenBveApi.Colors;
using OpenBveApi.Interface;
using OpenBveApi.Packages;
using OpenBveApi.Textures;
using RouteManager2;
using Path = OpenBveApi.Path;

namespace OpenBve
{
	public partial class Menu
	{
		private static BackgroundWorker routeWorkerThread;
		private static BackgroundWorker packageWorkerThread;
		private static string SearchDirectory;
		private static string currentFile;
		private static Encoding RouteEncoding;
		private static RouteState RoutefileState;
		private static readonly Picturebox routePictureBox = new Picturebox(Program.Renderer);
		private static readonly Textbox routeDescriptionBox = new Textbox(Program.Renderer, Program.Renderer.Fonts.NormalFont, Color128.White, Color128.Black);
		private static Dictionary<string, Texture> iconCache = new Dictionary<string, Texture>();
		private static Package currentPackage;
		private static PackageOperation currentOperation;
		private static bool packagePreview;

		private static void packageWorkerThread_doWork(object sender, DoWorkEventArgs e)
		{
			if (string.IsNullOrEmpty(currentFile))
			{
				return;
			}
			
			switch (currentOperation)
			{
				case PackageOperation.Installing:
					if (packagePreview)
					{
						try
						{
							currentPackage = Manipulation.ReadPackage(currentFile);
						}
						catch
						{
							// Ignored
						}
						
					}
					else
					{
						if (currentPackage != null)
						{
							switch (currentPackage.PackageType)
							{
								case PackageType.Route:
									break;
								case PackageType.Train:
									break;
								case PackageType.Other:
									break;
							}
						}
					}
					break;
				case PackageOperation.Uninstalling:
					break;
			}
		}

		private static void packageWorkerThread_completed(object sender, RunWorkerCompletedEventArgs e)
		{
			if (currentPackage != null)
			{
				routePictureBox.Texture = new Texture(new Bitmap(currentPackage.PackageImage));
				routeDescriptionBox.Text = currentPackage.Description;
				packagePreview = false;
			}
		}

		private static void routeWorkerThread_doWork(object sender, DoWorkEventArgs e)
		{
			if (string.IsNullOrEmpty(currentFile))
			{
				return;
			}
			RouteEncoding = TextEncoding.GetSystemEncodingFromFile(currentFile);
			Program.CurrentHost.RegisterTexture(Path.CombineFile(Program.FileSystem.DataFolder, "Menu\\loading.png"), new TextureParameters(null, null), out routePictureBox.Texture);
			routeDescriptionBox.Text = Translations.GetInterfaceString("start_route_processing");
			Game.Reset(false);
			bool loaded = false;
			for (int i = 0; i < Program.CurrentHost.Plugins.Length; i++)
			{
				if (Program.CurrentHost.Plugins[i].Route != null && Program.CurrentHost.Plugins[i].Route.CanLoadRoute(currentFile))
				{
					object Route = (object)Program.CurrentRoute; //must cast to allow us to use the ref keyword.
					string RailwayFolder = Loading.GetRailwayFolder(currentFile);
					string ObjectFolder = OpenBveApi.Path.CombineDirectory(RailwayFolder, "Object");
					string SoundFolder = OpenBveApi.Path.CombineDirectory(RailwayFolder, "Sound");
					if (Program.CurrentHost.Plugins[i].Route.LoadRoute(currentFile, RouteEncoding, null, ObjectFolder, SoundFolder, true, ref Route))
					{
						Program.CurrentRoute = (CurrentRoute) Route;
					}
					else
					{
						if (Program.CurrentHost.Plugins[i].Route.LastException != null)
						{
							throw Program.CurrentHost.Plugins[i].Route.LastException; //Re-throw last exception generated by the route parser plugin so that the UI thread captures it
						}
						routeDescriptionBox.Text = "An unknown error was enountered whilst attempting to parse the routefile " + currentFile;
						RoutefileState = RouteState.Error;
					}
					loaded = true;
					break;
				}
			}

			if (!loaded)
			{
				throw new Exception("No plugins capable of loading routefile " + currentFile + " were found.");
			}
		}

		private static void routeWorkerThread_completed(object sender, RunWorkerCompletedEventArgs e)
		{
			RoutefileState = RouteState.Processed;
			if (e.Error != null || Program.CurrentRoute == null)
			{
				Program.CurrentHost.RegisterTexture(Path.CombineFile(Program.FileSystem.DataFolder, "Menu\\route_error.png"), new TextureParameters(null, null), out routePictureBox.Texture);
				if (e.Error != null)
				{
					routeDescriptionBox.Text = e.Error.Message;
					RoutefileState = RouteState.Error;
				}
				routeWorkerThread.Dispose();
				return;
			}
			try
			{
				// image
				if (!string.IsNullOrEmpty(Program.CurrentRoute.Image))
				{

					try
					{
						if (File.Exists(Program.CurrentRoute.Image))
						{
							Program.CurrentHost.RegisterTexture(Program.CurrentRoute.Image, new TextureParameters(null, null), out routePictureBox.Texture);
						}
						else
						{
							Program.CurrentHost.RegisterTexture(Path.CombineFile(Program.FileSystem.DataFolder, "Menu\\route_unknown.png"), new TextureParameters(null, null), out routePictureBox.Texture);
						}
						
					}
					catch
					{
						routePictureBox.Texture = null;
					}
				}
				else
				{
					string[] f = {".png", ".bmp", ".gif", ".tiff", ".tif", ".jpeg", ".jpg"};
					int i;
					for (i = 0; i < f.Length; i++)
					{
						string g = OpenBveApi.Path.CombineFile(System.IO.Path.GetDirectoryName(currentFile),
							System.IO.Path.GetFileNameWithoutExtension(currentFile) + f[i]);
						if (System.IO.File.Exists(g))
						{
							try
							{
								using (var fs = new FileStream(g, FileMode.Open, FileAccess.Read))
								{
									//pictureboxRouteImage.Image = new Bitmap(fs);
								}
							}
							catch
							{
								//pictureboxRouteImage.Image = null;
							}
							break;
						}
					}
					if (i == f.Length)
					{
						Program.CurrentHost.RegisterTexture(Path.CombineFile(Program.FileSystem.DataFolder, "Menu\\route_unknown.png"), new TextureParameters(null, null), out routePictureBox.Texture);
					}
				}

				// description
				string Description = Program.CurrentRoute.Comment.ConvertNewlinesToCrLf();
				if (Description.Length != 0)
				{
					routeDescriptionBox.Text = Description;
				}
				else
				{
					routeDescriptionBox.Text = System.IO.Path.GetFileNameWithoutExtension(currentFile);
				}
			}
			catch (Exception ex)
			{
				Program.CurrentHost.RegisterTexture(Path.CombineFile(Program.FileSystem.DataFolder, "Menu\\route_error.png"), new TextureParameters(null, null), out routePictureBox.Texture);
				routeDescriptionBox.Text = ex.Message;
				currentFile = null;
			}
		}
	}
}
