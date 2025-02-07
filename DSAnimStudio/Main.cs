﻿using DSAnimStudio.DbgMenus;
using DSAnimStudio.GFXShaders;
using DSAnimStudio.ImguiOSD;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SharpDX.DirectWrite;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DSAnimStudio
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Main : Game
    {
        public const string VERSION = "Version 3.0.6 - Age of Meows [WIP]";

        public static T ReloadMonoGameContent<T>(string path)
        {
            path = Path.GetFullPath(path);

            if (path.ToLower().EndsWith(".xnb"))
                path = path.Substring(0, path.Length - 4);

            return MainContentManager.Load<T>(path);
        }

        public static void CenterForm(Form form)
        {
            var x = Main.WinForm.Location.X + (Main.WinForm.Width - form.Width) / 2;
            var y = Main.WinForm.Location.Y + (Main.WinForm.Height - form.Height) / 2;
            form.Location = new System.Drawing.Point(Math.Max(x, 0), Math.Max(y, 0));
        }

        protected override void Dispose(bool disposing)
        {
            WindowsMouseHook.Unhook();

            RemoManager.DisposeAllModels();

            base.Dispose(disposing);
        }

        public static bool IgnoreSizeChanges = false;

        public static Rectangle LastBounds = Rectangle.Empty;
        private static Rectangle lastActualBounds = Rectangle.Empty;

        public static ColorConfig Colors = new ColorConfig();

        public static Form WinForm;

        public static float DPICustomMultX = 1;
        public static float DPICustomMultY = 1;

        private static float BaseDPIX = 1;
        private static float BaseDPIY = 1;

        public static float DPIX => BaseDPIX * DPICustomMultX;
        public static float DPIY => BaseDPIY * DPICustomMultY;

        public static FancyInputHandler Input;

        public const int ConfigFileIOMaxTries = 10;

        public static bool NeedsToLoadConfigFileForFirstTime { get; private set; } = true;

        public static bool DisableConfigFileAutoSave = false;

        private static object _lock_actualConfig = new object();
        private static TaeEditor.TaeConfigFile actualConfig = new TaeEditor.TaeConfigFile();
        public static TaeEditor.TaeConfigFile Config
        {
            get
            {
                TaeEditor.TaeConfigFile result = null;
                lock (_lock_actualConfig)
                {
                    result = actualConfig;
                }
                return result;
            }
            set
            {
                lock (_lock_actualConfig)
                {
                    actualConfig = value;
                }
            }
        }

        private static string ConfigFilePath = null;

        public const string ConfigFileShortName = "DSAnimStudio_Config.json";

        private static void CheckConfigFilePath()
        {
            if (ConfigFilePath == null)
            {
                var currentAssemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var currentAssemblyDir = System.IO.Path.GetDirectoryName(currentAssemblyPath);
                ConfigFilePath = System.IO.Path.Combine(currentAssemblyDir, ConfigFileShortName);
            }
        }

        public static void LoadConfig(bool isManual = false)
        {
            if (!isManual && DisableConfigFileAutoSave)
                return;
            CheckConfigFilePath();
            if (!System.IO.File.Exists(ConfigFilePath))
            {
                Config = new TaeEditor.TaeConfigFile();
                SaveConfig();
            }
            string jsonText = null;
            int tryCounter = 0;

            while (jsonText == null)
            {
                bool giveUp = false;
                try
                {
                    if (tryCounter < ConfigFileIOMaxTries)
                    {
                        jsonText = System.IO.File.ReadAllText(ConfigFilePath);
                    }
                    else
                    {
                        var ask = System.Windows.Forms.MessageBox.Show($"Failed 10 times in a row to read configuration file '{ConfigFileShortName}' from the application folder. " +
                            "It may have been in use by another " +
                            "application (e.g. another instance of DS Anim Studio). " +
                            "\n\nWould you like to RETRY the configuration file reading operation or CANCEL, " +
                            "disabling configuration file autosaving to be safe?", "Configuration File IO Failure",
                            MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);

                        if (ask == DialogResult.Retry)
                        {
                            giveUp = false;
                            tryCounter = 0;
                        }
                        else
                        {
                            giveUp = true;
                        }
                    }
                }
                catch
                {
                    tryCounter++;
                }

                if (giveUp)
                {
                    DisableConfigFileAutoSave = true;
                    return;
                }
            }

            try
            {
                Config = Newtonsoft.Json.JsonConvert.DeserializeObject<TaeEditor.TaeConfigFile>(jsonText);

                
            }
            catch (Newtonsoft.Json.JsonException)
            {
                var ask = System.Windows.Forms.MessageBox.Show($"Failed to parse configuration file '{ConfigFileShortName}' in the application folder. " +
                            "It may have been saved by an incompatible version of the application or corrupted. " +
                            "\n\nWould you like to overwrite it with default settings? " +
                            "\n\nIf not, configuration file autosaving will be disabled to keep the file as-is.", "Configuration File Parse Failure",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (ask == DialogResult.Yes)
                {
                    Config = new TaeEditor.TaeConfigFile();
                    SaveConfig(isManual: true);
                }
                else
                {
                    DisableConfigFileAutoSave = true;
                }
            }

            Config.AfterLoading(TAE_EDITOR);

            if (NeedsToLoadConfigFileForFirstTime)
            {
                Config.AfterLoadingFirstTime(TAE_EDITOR);
            }

            NeedsToLoadConfigFileForFirstTime = false;
        }

        public static void SaveConfig(bool isManual = false)
        {

            if (!isManual && DisableConfigFileAutoSave)
                return;
            lock (Main.Config._lock_ThreadSensitiveStuff)
            {
                if (TAE_EDITOR?.Graph != null)
                {
                    // I'm sorry; this is pecularily placed.
                    TAE_EDITOR.Graph?.ViewportInteractor?.SaveChrAsm();
                }

                Config.BeforeSaving(TAE_EDITOR);
                CheckConfigFilePath();

                var jsonText = Newtonsoft.Json.JsonConvert
                    .SerializeObject(Config,
                    Newtonsoft.Json.Formatting.Indented);

                bool success = false;

                int tryCounter = 0;

                while (!success)
                {
                    bool giveUp = false;
                    try
                    {
                        if (tryCounter < ConfigFileIOMaxTries)
                        {
                            System.IO.File.WriteAllText(ConfigFilePath, jsonText);
                            success = true;
                        }
                        else
                        {
                            var ask = System.Windows.Forms.MessageBox.Show($"Failed 10 times in a row to write configuration file '{ConfigFileShortName}' in the application folder. " +
                                "It may have been in use by another " +
                                "application (e.g. another instance of DS Anim Studio). " +
                                "\n\nWould you like to RETRY the configuration file writing operation or CANCEL, " +
                                "disabling configuration file autosaving to be safe?", "Configuration File IO Failure",
                                MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);

                            if (ask == DialogResult.Retry)
                            {
                                giveUp = false;
                                tryCounter = 0;
                            }
                            else
                            {
                                giveUp = true;
                            }
                        }
                    }
                    catch
                    {
                        tryCounter++;
                    }

                    if (giveUp)
                    {
                        DisableConfigFileAutoSave = true;
                        return;
                    }
                }

            }
        }

        public static Vector2 DPIVector => new Vector2(DPIX, DPIY);
        public static System.Numerics.Vector2 DPIVectorN => new System.Numerics.Vector2(DPIX, DPIY);

        public static Matrix DPIMatrix => Matrix.CreateScale(DPIX, DPIY, 1);

        public static Random Rand = new Random();
        public static float RandFloat()
        {
            return (float)Rand.NextDouble();
        }
        public static float RandSignedFloat()
        {
            return (float)((Rand.NextDouble() * 2) - 1);
        }
        public static Vector3 RandSignedVector3()
        {
            return new Vector3(RandSignedFloat(), RandSignedFloat(), RandSignedFloat());
        }

        public static string Directory = null;

        

        public static bool FIXED_TIME_STEP = false;

        public static bool REQUEST_EXIT = false;

        public static float DELTA_UPDATE;
        public static float DELTA_UPDATE_ROUNDED;
        public static float DELTA_DRAW;

        public static ImGuiRenderer ImGuiDraw;

        public static Vector2 GlobalTaeEditorFontOffset = new Vector2(0, -3);

        public static IServiceProvider MainContentServiceProvider = null;

        private bool prevFrameWasLoadingTaskRunning = false;
        public static bool Active { get; private set; }
        public static HysteresisBool ActiveHyst = new HysteresisBool(0, 5);
        public static bool prevActive { get; private set; }

        public static bool IsFirstUpdateLoop { get; private set; } = true;
        public static bool IsFirstFrameActive { get; private set; } = false;

        public static bool Minimized { get; private set; }

        public static bool DISABLE_DRAW_ERROR_HANDLE = true;

        private static float MemoryUsageCheckTimer = 0;
        private static long MemoryUsage_Unmanaged = 0;
        private static long MemoryUsage_Managed = 0;
        private const float MemoryUsageCheckInterval = 0.25f;

        public static readonly Color SELECTED_MESH_COLOR = Color.Yellow * 0.05f;
        //public static readonly Color SELECTED_MESH_WIREFRAME_COLOR = Color.Yellow;

        public static Texture2D WHITE_TEXTURE;
        public static Texture2D DEFAULT_TEXTURE_DIFFUSE;
        public static Texture2D DEFAULT_TEXTURE_SPECULAR;
        public static Texture2D DEFAULT_TEXTURE_SPECULAR_DS2;
        public static Texture2D DEFAULT_TEXTURE_NORMAL;
        public static Texture2D DEFAULT_TEXTURE_NORMAL_DS2;
        public static Texture2D DEFAULT_TEXTURE_MISSING;
        public static TextureCube DEFAULT_TEXTURE_MISSING_CUBE;
        public static Texture2D DEFAULT_TEXTURE_EMISSIVE;
        public string DEFAULT_TEXTURE_MISSING_NAME => $@"{Main.Directory}\Content\Utility\MissingTexture";

        public static TaeEditor.TaeEditorScreen TAE_EDITOR;
        private static SpriteBatch TaeEditorSpriteBatch;
        public static Texture2D TAE_EDITOR_BLANK_TEX;
        public static SpriteFont TAE_EDITOR_FONT;
        public static SpriteFont TAE_EDITOR_FONT_SMALL;
        public static Texture2D TAE_EDITOR_SCROLLVIEWER_ARROW;

        public static FlverTonemapShader MainFlverTonemapShader = null;

        //public static Stopwatch UpdateStopwatch = new Stopwatch();
        //public static TimeSpan MeasuredTotalTime = TimeSpan.Zero;
        //public static TimeSpan MeasuredElapsedTime = TimeSpan.Zero;

        public bool IsLoadingTaskRunning = false;
        public HysteresisBool IsLoadingTaskRunningHyst = new HysteresisBool(0, 5);

        public static ContentManager MainContentManager = null;

        public static RenderTarget2D SceneRenderTarget = null;
        //public static RenderTarget2D UnusedRendertarget0 = null;
        public static int UnusedRenderTarget0Padding = 0;

        public static int RequestHideOSD = 0;
        public static int RequestHideOSD_MAX = 10;

        public static bool RequestViewportRenderTargetResolutionChange = false;
        private const float TimeBeforeNextRenderTargetUpdate_Max = 0.5f;
        private static float TimeBeforeNextRenderTargetUpdate = 0;

        public static ImFontPtr ImGuiFontPointer;

        public Rectangle TAEScreenBounds
        {
            get => GFX.Device.Viewport.Bounds;
            set
            {
                if (value != TAEScreenBounds)
                {
                    GFX.Device.Viewport = new Viewport(value);
                }
            }
        }

        public Rectangle ClientBounds => TAE_EDITOR.ModelViewerBounds;

        private static GraphicsDeviceManager graphics;
        //public ContentManager Content;
        //public bool IsActive = true;

        public static List<DisplayMode> GetAllResolutions()
        {
            List<DisplayMode> result = new List<DisplayMode>();
            foreach (var mode in GraphicsAdapter.DefaultAdapter.SupportedDisplayModes)
            {
                result.Add(mode);
            }
            return result;
        }

        public static void ApplyPresentationParameters(int width, int height, SurfaceFormat format,
            bool vsync, bool fullscreen)
        {
            graphics.PreferredBackBufferWidth = width;
            graphics.PreferredBackBufferHeight = height;
            graphics.PreferredBackBufferFormat = GFX.BackBufferFormat;
            graphics.IsFullScreen = fullscreen;
            graphics.SynchronizeWithVerticalRetrace = vsync;

            if (GFX.MSAA > 0)
            {
                graphics.PreferMultiSampling = true;
                graphics.GraphicsDevice.PresentationParameters.MultiSampleCount = GFX.MSAA;
            }
            else
            {
                graphics.PreferMultiSampling = false;
                graphics.GraphicsDevice.PresentationParameters.MultiSampleCount = 1;
            }

            //graphics.PreferMultiSampling = false;
            //graphics.GraphicsDevice.PresentationParameters.MultiSampleCount = 1;

            graphics.ApplyChanges();
        }



        //MCG MCGTEST_MCG;



        public Main()
        {
            

            WinForm = (Form)Form.FromHandle(Window.Handle);

            WinForm.KeyPreview = true;
            

            WindowsMouseHook.Hook(Window.Handle);
            WinForm.AutoScaleMode = AutoScaleMode.Dpi;

            WinForm.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 0);

            BaseDPIX = BaseDPIY = WinForm.DeviceDpi / 96f;
            WinForm.DpiChanged += WinForm_DpiChanged;

            Directory = new FileInfo(typeof(Main).Assembly.Location).DirectoryName;

            graphics = new GraphicsDeviceManager(this);
            graphics.DeviceCreated += Graphics_DeviceCreated;
            graphics.DeviceReset += Graphics_DeviceReset;

            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromTicks(166667);
            // Setting this max higher allows it to skip frames instead of do slow motion.
            MaxElapsedTime = TimeSpan.FromSeconds(0.5);

            //IsFixedTimeStep = false;
            graphics.SynchronizeWithVerticalRetrace = GFX.Display.Vsync;
            graphics.IsFullScreen = GFX.Display.Fullscreen;
            //graphics.PreferMultiSampling = GFX.Display.SimpleMSAA;
            graphics.PreferredBackBufferWidth = (int)Math.Round(GFX.Display.Width * DPIX);
            graphics.PreferredBackBufferHeight = (int)Math.Round(GFX.Display.Height * DPIY);
            if (!GraphicsAdapter.DefaultAdapter.IsProfileSupported(GraphicsProfile.HiDef))
            {
                System.Windows.Forms.MessageBox.Show("MonoGame is detecting your GPU as too " +
                    "low-end and refusing to enter the non-mobile Graphics Profile, " +
                    "which is needed for the model viewer. The app will likely crash now.");

                graphics.GraphicsProfile = GraphicsProfile.Reach;
            }
            else
            {
                graphics.GraphicsProfile = GraphicsProfile.HiDef;
            }

            graphics.PreferredBackBufferFormat = GFX.BackBufferFormat;

            graphics.PreferMultiSampling = false;

            graphics.ApplyChanges();

            Window.AllowUserResizing = true;

            Window.ClientSizeChanged += Window_ClientSizeChanged;

            WinForm.Shown += (o, e) =>
            {
                LoadConfig();
                FmodManager.InitTest();
            };

            this.Activated += Main_Activated;
            this.Deactivated += Main_Deactivated;

            GFX.Display.SetFromDisplayMode(GraphicsAdapter.DefaultAdapter.CurrentDisplayMode);


            Input = new FancyInputHandler();
            //GFX.Device.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
        }

        private void WinForm_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            UpdateDpiStuff();
        }

        public void UpdateDpiStuff()
        {
            float newDpi = WinForm.DeviceDpi / 96f;
            BaseDPIX = BaseDPIY = newDpi;

            RequestViewportRenderTargetResolutionChange = true;
        }

        //protected override void Dispose(bool disposing)
        //{
        //    if (disposing)
        //        FmodManager.Shutdown();

        //    base.Dispose(disposing);
        //}

        private void Main_Deactivated(object sender, EventArgs e)
        {
            UpdateActiveState();
            FmodManager.StopAllSounds();
        }

        private void Main_Activated(object sender, EventArgs e)
        {
            IsFirstFrameActive = true;
            UpdateActiveState();
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            if (IgnoreSizeChanges)
                return;

            RequestHideOSD = RequestHideOSD_MAX;
            UpdateActiveState();

            TAE_EDITOR?.HandleWindowResize(lastActualBounds, Window.ClientBounds);

            if (Window.ClientBounds.Width > 0 && Window.ClientBounds.Height > 0)
                LastBounds = Window.ClientBounds;
            lastActualBounds = Window.ClientBounds;
        }

        public void RebuildRenderTarget()
        {
            if (TimeBeforeNextRenderTargetUpdate <= 0)
            {
                GFX.ClampAntialiasingOptions();

                int msaa = GFX.MSAA;
                int ssaa = GFX.SSAA;



                SceneRenderTarget?.Dispose();
                //UnusedRendertarget0?.Dispose();
                //GFX.BokehRenderTarget?.Dispose();

                GC.Collect();

                SceneRenderTarget = new RenderTarget2D(GFX.Device, TAE_EDITOR.ModelViewerBounds.DpiScaled().Width * ssaa,
                       TAE_EDITOR.ModelViewerBounds.DpiScaled().Height * ssaa, ssaa > 1, SurfaceFormat.Vector4, DepthFormat.Depth24,
                       ssaa > 1 ? 1 : msaa, RenderTargetUsage.DiscardContents);

                //UnusedRendertarget0 = new RenderTarget2D(GFX.Device, TAE_EDITOR.ModelViewerBounds.DpiScaled().Width + (UnusedRenderTarget0Padding * 2),
                //       TAE_EDITOR.ModelViewerBounds.DpiScaled().Height + (UnusedRenderTarget0Padding * 2), true, SurfaceFormat.Vector4, DepthFormat.Depth24,
                //       1, RenderTargetUsage.DiscardContents);

                //GFX.BokehRenderTarget = new RenderTarget2D(GFX.Device, TAE_EDITOR.ModelViewerBounds.DpiScaled().Width + (UnusedRenderTarget0Padding * 2),
                //       TAE_EDITOR.ModelViewerBounds.DpiScaled().Height + (UnusedRenderTarget0Padding * 2), true, SurfaceFormat.Vector4, DepthFormat.Depth24,
                //       1, RenderTargetUsage.DiscardContents);

                TimeBeforeNextRenderTargetUpdate = TimeBeforeNextRenderTargetUpdate_Max;

                RequestViewportRenderTargetResolutionChange = false;

                GFX.EffectiveSSAA = ssaa;
                GFX.EffectiveMSAA = msaa;
            }
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            CFG.Save();

            Main.SaveConfig();

            base.OnExiting(sender, args);
        }

        private void Graphics_DeviceCreated(object sender, System.EventArgs e)
        {
            GFX.Device = GraphicsDevice;
        }

        private void Graphics_DeviceReset(object sender, System.EventArgs e)
        {
            GFX.Device = GraphicsDevice;
        }

        protected override void Initialize()
        {
            try
            {
                var winForm = (Form)Control.FromHandle(Window.Handle);
                winForm.AllowDrop = true;
                winForm.DragEnter += GameWindowForm_DragEnter;
                winForm.DragDrop += GameWindowForm_DragDrop;


                IsMouseVisible = true;

                DEFAULT_TEXTURE_DIFFUSE = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_DIFFUSE.SetData(new Color[] { new Color(0.5f, 0.5f, 0.5f) });

                WHITE_TEXTURE = new Texture2D(GraphicsDevice, 1, 1);
                WHITE_TEXTURE.SetData(new Color[] { new Color(1.0f, 1.0f, 1.0f) });

                DEFAULT_TEXTURE_SPECULAR = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_SPECULAR.SetData(new Color[] { new Color(0.5f, 0.5f, 0.5f) });

                DEFAULT_TEXTURE_SPECULAR_DS2 = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_SPECULAR_DS2.SetData(new Color[] { new Color(0.5f, 0.5f, 0.5f) });

                DEFAULT_TEXTURE_NORMAL = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_NORMAL.SetData(new Color[] { new Color(0.5f, 0.5f, 0.0f) });

                DEFAULT_TEXTURE_NORMAL_DS2 = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_NORMAL_DS2.SetData(new Color[] { new Color(0.5f, 0.5f, 0.5f, 0.5f) });

                DEFAULT_TEXTURE_EMISSIVE = new Texture2D(GraphicsDevice, 1, 1);
                DEFAULT_TEXTURE_EMISSIVE.SetData(new Color[] { Color.Black });

                DEFAULT_TEXTURE_MISSING = Content.Load<Texture2D>(DEFAULT_TEXTURE_MISSING_NAME);

                DEFAULT_TEXTURE_MISSING_CUBE = new TextureCube(GraphicsDevice, 1, false, SurfaceFormat.Color);
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.PositiveX, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.PositiveY, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.PositiveZ, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.NegativeX, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.NegativeY, new Color[] { Color.Fuchsia });
                DEFAULT_TEXTURE_MISSING_CUBE.SetData(CubeMapFace.NegativeZ, new Color[] { Color.Fuchsia });

                GFX.Device = GraphicsDevice;

                ImGuiDraw = new ImGuiRenderer(this);
                //ImGuiDraw.RebuildFontAtlas();

                base.Initialize();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"Error occurred while initializing DS Anim Studio (please report):\n\n{ex.ToString()}", 
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }

        

        private static Microsoft.Xna.Framework.Vector3 currModelAddOffset = Microsoft.Xna.Framework.Vector3.Zero;
        private void GameWindowForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] modelFiles = (string[])e.Data.GetData(DataFormats.FileDrop, false);

            //TAE_EDITOR.

            void LoadOneFile(string file)
            {
                if (file.ToUpper().EndsWith(".FLVER") || file.ToUpper().EndsWith(".FLVER.DCX"))
                {
                    if (FLVER2.Is(file))
                    {
                        //currModelAddOffset.X += 3;
                        var m = new Model(FLVER2.Read(file), false);
                        m.StartTransform = new Transform(currModelAddOffset, Microsoft.Xna.Framework.Quaternion.Identity);
                        Scene.ClearSceneAndAddModel(m);
                    }
                    else if (FLVER0.Is(file))
                    {
                        //currModelAddOffset.X += 3;
                        var m = new Model(FLVER0.Read(file), false);
                        m.StartTransform = new Transform(currModelAddOffset, Microsoft.Xna.Framework.Quaternion.Identity);
                        Scene.ClearSceneAndAddModel(m);
                    }
                }
                else if (file.ToUpper().EndsWith(".CHRBND") || file.ToUpper().EndsWith(".CHRBND.DCX"))
                {
                    Scene.ClearScene();
                    //currModelAddOffset.X += 3;
                    GameDataManager.InitializeFromBND(file);
                    var m = GameDataManager.LoadCharacter(Utils.GetShortIngameFileName(file));
                    m.StartTransform = m.CurrentTransform = new Transform(currModelAddOffset, Microsoft.Xna.Framework.Quaternion.Identity);
                    m.AnimContainer.CurrentAnimationName = m.AnimContainer.Animations.Keys.FirstOrDefault();
                    m.AnimContainer.ForcePlayAnim = true;
                    m.UpdateAnimation();
                    //Scene.ClearSceneAndAddModel(m);
                }
                else if (file.ToUpper().EndsWith(".OBJBND") || file.ToUpper().EndsWith(".OBJBND.DCX"))
                {
                    Scene.ClearScene();
                    //currModelAddOffset.X += 3;
                    GameDataManager.InitializeFromBND(file);
                    var m = GameDataManager.LoadObject(Utils.GetShortIngameFileName(file));
                    m.StartTransform = m.CurrentTransform = new Transform(currModelAddOffset, Microsoft.Xna.Framework.Quaternion.Identity);
                    m.AnimContainer.CurrentAnimationName = m.AnimContainer.Animations.Keys.FirstOrDefault();
                    m.AnimContainer.ForcePlayAnim = true;
                    m.UpdateAnimation();
                    //Scene.ClearSceneAndAddModel(m);
                }
                else if (file.ToUpper().EndsWith(".HKX"))
                {
                    var anim = KeyboardInput.Show("Enter Anim ID", "Enter name to save the dragged and dropped HKX file to e.g. a01_3000.");
                    string name = anim.Result;
                    byte[] animData = File.ReadAllBytes(file);
                    TAE_EDITOR.FileContainer.AddNewHKX(name, animData, out byte[] dataForAnimContainer);
                    TAE_EDITOR.Graph.ViewportInteractor.CurrentModel.AnimContainer.AddNewHKXToLoad(name + ".hkx", dataForAnimContainer);
                }
            }

            if (modelFiles.Length == 1)
            {
                string f = modelFiles[0].ToLower();

                if (f.EndsWith(".fbx"))
                {
                    TAE_EDITOR.Config.LastUsedImportConfig_FLVER2.AssetPath = modelFiles[0];
                    TAE_EDITOR.BringUpImporter_FLVER2();
                    TAE_EDITOR.Config.LastUsedImportConfig_FLVER2.AssetPath = modelFiles[0];
                    TAE_EDITOR.ImporterWindow_FLVER2.LoadValuesFromConfig();
                }
                else
                {
                    LoadOneFile(modelFiles[0]);
                }
            }
            else
            {

                LoadingTaskMan.DoLoadingTask("LoadingDroppedModel", "Loading dropped model(s)...", prog =>
                {
                    foreach (var file in modelFiles)
                    {
                        LoadOneFile(file);
                    }

                }, disableProgressBarByDefault: true);
            }

            //LoadDragDroppedFiles(modelFiles.ToDictionary(f => f, f => File.ReadAllBytes(f)));
        }

        static bool IsValidDragDropModelFile(string f)
        {
            return (BND3.Is(f) || BND4.Is(f) || FLVER2.Is(f));
        }

        private void GameWindowForm_DragEnter(object sender, DragEventArgs e)
        {
            IsFirstFrameActive = true;

            bool isValid = false;

            if (e.Data.GetDataPresent(DataFormats.FileDrop) && !LoadingTaskMan.AnyTasksRunning())
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop, false);
                

                if (files.Length == 1)
                {
                    string f = files[0].ToLower();

                    if (f.EndsWith(".fbx"))
                    {
                        isValid = Scene.IsModelLoaded;
                    }
                    else if (f.EndsWith(".flver.dcx") || f.EndsWith(".flver") || f.EndsWith(".chrbnd") || f.EndsWith(".chrbnd.dcx") || f.EndsWith(".objbnd") || f.EndsWith(".objbnd.dcx"))
                    {
                        isValid = true;
                    }
                }
                // If multiple files are dragged they must all be regularly 
                // loadable rather than the specific case ones above
                else if (files.All(f => IsValidDragDropModelFile(f)))
                    isValid = true;


            }

            e.Effect = isValid ? DragDropEffects.Link : DragDropEffects.None;
        }

        public static Rectangle GetTaeEditorRect()
        {
            return new Rectangle(0, 0, GFX.Device.Viewport.Width, GFX.Device.Viewport.Height - 2);
        }

        protected override void LoadContent()
        {
            MainContentServiceProvider = Content.ServiceProvider;
            MainContentManager = Content;

            GFX.Init(Content);
            DBG.LoadContent(Content);
            //InterrootLoader.OnLoadError += InterrootLoader_OnLoadError;

            DBG.CreateDebugPrimitives();

            //DBG.EnableMenu = true;
            //DBG.EnableMouseInput = true;
            //DBG.EnableKeyboardInput = true;
            //DbgMenuItem.Init();

            UpdateMemoryUsage();

            CFG.AttemptLoadOrDefault();

            TAE_EDITOR_FONT = Content.Load<SpriteFont>($@"{Main.Directory}\Content\Fonts\DbgMenuFontSmall");
            TAE_EDITOR_FONT_SMALL = Content.Load<SpriteFont>($@"{Main.Directory}\Content\Fonts\DbgMenuFontSmaller");
            TAE_EDITOR_BLANK_TEX = new Texture2D(GraphicsDevice, 1, 1, false, SurfaceFormat.Color);
            TAE_EDITOR_BLANK_TEX.SetData(new Color[] { Color.White }, 0, 1);
            TAE_EDITOR_SCROLLVIEWER_ARROW = Content.Load<Texture2D>($@"{Main.Directory}\Content\Utility\TaeEditorScrollbarArrow");

            TAE_EDITOR = new TaeEditor.TaeEditorScreen((Form)Form.FromHandle(Window.Handle), GetTaeEditorRect());

            TaeEditorSpriteBatch = new SpriteBatch(GFX.Device);

            if (Program.ARGS.Length > 0)
            {
                TAE_EDITOR.FileContainerName = Program.ARGS[0];

                LoadingTaskMan.DoLoadingTask("ProgramArgsLoad", "Loading ANIBND and associated model(s)...", progress =>
                {
                    TAE_EDITOR.LoadCurrentFile();
                }, disableProgressBarByDefault: true);

                //LoadDragDroppedFiles(Program.ARGS.ToDictionary(f => f, f => File.ReadAllBytes(f)));
            }

            MainFlverTonemapShader = new FlverTonemapShader(Content.Load<Effect>($@"Content\Shaders\FlverTonemapShader"));

            BuildImguiFonts();

            TAE_EDITOR.LoadContent(Content);

            UpdateDpiStuff();
        }

        private static void BuildImguiFonts()
        {
            var fonts = ImGuiNET.ImGui.GetIO().Fonts;
            var fontFile = File.ReadAllBytes($@"{Directory}\Content\Fonts\NotoSansCJKjp-Medium.otf");
            fonts.Clear();
            unsafe
            {
                fixed (byte* p = fontFile)
                {
                    ImVector ranges;
                    ImFontGlyphRangesBuilder* rawPtr = ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder();
                    var builder = new ImFontGlyphRangesBuilderPtr(rawPtr);
                    var ccm = CCM.Read($@"{Directory}\Content\Fonts\dbgfont14h_ds3.ccm");
                    foreach (var g in ccm.Glyphs)
                        builder.AddChar((ushort)g.Key);

                    builder.BuildRanges(out ranges);
                    var ptr = ImGuiNET.ImGuiNative.ImFontConfig_ImFontConfig();
                    var cfg = new ImGuiNET.ImFontConfigPtr(ptr);
                    cfg.GlyphMinAdvanceX = 5.0f;
                    cfg.OversampleH = 5;
                    cfg.OversampleV = 5;
                    cfg.PixelSnapH = true;
                    ImGuiFontPointer = fonts.AddFontFromMemoryTTF((IntPtr)p, fontFile.Length, 18, cfg, ranges.Data);
                }
            }
            fonts.Build();
            ImGuiDraw.RebuildFontAtlas();
        }

        private void InterrootLoader_OnLoadError(string contentName, string error)
        {
            Console.WriteLine($"CONTENT LOAD ERROR\nCONTENT NAME:{contentName}\nERROR:{error}");
        }

        private string GetMemoryUseString(string prefix, long MemoryUsage)
        {
            const double MEM_KB = 1024f;
            const double MEM_MB = 1024f * 1024f;
            //const double MEM_GB = 1024f * 1024f * 1024f;

            if (MemoryUsage < MEM_KB)
                return $"{prefix}{(1.0 * MemoryUsage):0} B";
            else if (MemoryUsage < MEM_MB)
                return $"{prefix}{(1.0 * MemoryUsage / MEM_KB):0.00} KB";
            else// if (MemoryUsage < MEM_GB)
                return $"{prefix}{(1.0 * MemoryUsage / MEM_MB):0.00} MB";
            //else
            //    return $"{prefix}{(1.0 * MemoryUsage / MEM_GB):0.00} GB";
        }

        private Color GetMemoryUseColor(long MemoryUsage)
        {
            //const double MEM_KB = 1024f;
            //const double MEM_MB = 1024f * 1024f;
            const double MEM_GB = 1024f * 1024f * 1024f;

            if (MemoryUsage < MEM_GB)
                return Colors.GuiColorMemoryUseTextGood;
            else if (MemoryUsage < (MEM_GB * 2))
                return Colors.GuiColorMemoryUseTextOkay;
            else
                return Colors.GuiColorMemoryUseTextBad;
        }

        private void DrawMemoryUsage()
        {
            var str_managed = GetMemoryUseString("CLR Mem:  ", MemoryUsage_Managed);
            var str_unmanaged = GetMemoryUseString("RAM USE:  ", MemoryUsage_Unmanaged);

            var strSize_managed = DBG.DEBUG_FONT_SMALL.MeasureString(str_managed);
            var strSize_unmanaged = DBG.DEBUG_FONT_SMALL.MeasureString(str_unmanaged);

            //DBG.DrawOutlinedText(str_managed, new Vector2(GFX.Device.Viewport.Width - 2, 
            //    GFX.Device.Viewport.Height - (strSize_managed.Y * 0.75f) - (strSize_unmanaged.Y * 0.75f)),
            //    Color.Cyan, DBG.DEBUG_FONT_SMALL, scale: 0.75f, scaleOrigin: new Vector2(strSize_managed.X, 0));
            GFX.SpriteBatchBeginForText();
            DBG.DrawOutlinedText(str_managed, new Vector2(GFX.Device.Viewport.Width - 6,
                GFX.Device.Viewport.Height) / DPIVector,
                GetMemoryUseColor(MemoryUsage_Managed), DBG.DEBUG_FONT_SMALL, scale: 1, scaleOrigin: strSize_managed);
            GFX.SpriteBatchEnd();
        }

        private void UpdateMemoryUsage()
        {
            using (var proc = Process.GetCurrentProcess())
            {
                MemoryUsage_Unmanaged = proc.PrivateMemorySize64;
            }
            MemoryUsage_Managed = GC.GetTotalMemory(forceFullCollection: false);
        }

        private void UpdateActiveState()
        {
            Minimized = !(Window.ClientBounds.Width > 0 && Window.ClientBounds.Height > 0);

            Active = !Minimized && IsActive && ApplicationIsActivated();

            ActiveHyst.Update(Active);

            TargetElapsedTime = (ActiveHyst || LoadingTaskMan.AnyTasksRunning()) ? TimeSpan.FromTicks(166667) : TimeSpan.FromSeconds(0.25);

            if (!prevActive && Active)
            {
                IsFirstFrameActive = true;
            }

            prevActive = Active;
        }

        /// <summary>Returns true if the current application has focus, false otherwise</summary>
        public static bool ApplicationIsActivated()
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
            {
                return false;       // No window is currently activated
            }

            var procId = Process.GetCurrentProcess().Id;
            int activeProcId;
            GetWindowThreadProcessId(activatedHandle, out activeProcId);

            return activeProcId == procId;
        }


        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);


        protected override void Update(GameTime gameTime)
        {
            IsLoadingTaskRunning = LoadingTaskMan.AnyTasksRunning();
            IsLoadingTaskRunningHyst.Update(IsLoadingTaskRunning);

#if !DEBUG
            try
            {
#endif

            UpdateActiveState();

                if (ActiveHyst || LoadingTaskMan.AnyTasksRunning())
                {
                    GlobalInputState.Update();

                    DELTA_UPDATE = (float)gameTime.ElapsedGameTime.TotalSeconds;//(float)(Math.Max(gameTime.ElapsedGameTime.TotalMilliseconds, 10) / 1000.0);

                    //GFX.FlverDitherTime += DELTA_UPDATE;
                    //GFX.FlverDitherTime = GFX.FlverDitherTime % GFX.FlverDitherTimeMod;

                    if (!FIXED_TIME_STEP && GFX.AverageFPS >= 200)
                    {
                        DELTA_UPDATE_ROUNDED = (float)(Math.Max(gameTime.ElapsedGameTime.TotalMilliseconds, 10) / 1000.0);
                    }
                    else
                    {
                        DELTA_UPDATE_ROUNDED = DELTA_UPDATE;
                    }

                    if (!LoadingTaskMan.AnyTasksRunning()) {
                        Scene.UpdateAnimation();
                    }

                    float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;

                    LoadingTaskMan.Update(elapsed);

                    IsFixedTimeStep = FIXED_TIME_STEP;

                    if (DBG.EnableMenu)
                    {
                        DbgMenuItem.UpdateInput(elapsed);
                        DbgMenuItem.UICursorBlinkUpdate(elapsed);
                    }

                    //if (DbgMenuItem.MenuOpenState != DbgMenuOpenState.Open)
                    //{
                    //    // Only update input if debug menu isnt fully open.
                    //    GFX.World.UpdateInput(this, gameTime);
                    //}

                    

                    if (REQUEST_EXIT)
                        Exit();

                    MemoryUsageCheckTimer += elapsed;
                    if (MemoryUsageCheckTimer >= MemoryUsageCheckInterval)
                    {
                        MemoryUsageCheckTimer = 0;
                        UpdateMemoryUsage();
                    }


                    // BELOW IS TAE EDITOR STUFF

                    if (IsLoadingTaskRunning != prevFrameWasLoadingTaskRunning)
                    {
                        TAE_EDITOR.GameWindowAsForm.Invoke(new Action(() =>
                        {
                            if (IsLoadingTaskRunning)
                            {
                                Mouse.SetCursor(MouseCursor.Wait);
                            }

                            foreach (Control c in TAE_EDITOR.GameWindowAsForm.Controls)
                            {
                                c.Enabled = !IsLoadingTaskRunning;
                            }


                        }));

                        // Undo an infinite loading cursor on an aborted file load.
                        if (!IsLoadingTaskRunning)
                        {
                            Mouse.SetCursor(MouseCursor.Arrow);
                        }
                    }

                    if (!IsLoadingTaskRunning)
                    {
                        //MeasuredElapsedTime = UpdateStopwatch.Elapsed;
                        //MeasuredTotalTime = MeasuredTotalTime.Add(MeasuredElapsedTime);

                        //UpdateStopwatch.Restart();

                        if (!TAE_EDITOR.Rect.Contains(TAE_EDITOR.Input.MousePositionPoint))
                            TAE_EDITOR.Input.CursorType = MouseCursorType.Arrow;

                        
                        
                        if (Active)
                        {
                            if (Scene.CheckIfDrawing())
                                TAE_EDITOR.Update();
                        }
                        else
                        {
                            TAE_EDITOR.Input.CursorType = MouseCursorType.Arrow;
                        }
                        

                       

                        if (!string.IsNullOrWhiteSpace(TAE_EDITOR.FileContainerName))
                            Window.Title = $"{System.IO.Path.GetFileName(TAE_EDITOR.FileContainerName)}" +
                                $"{(TAE_EDITOR.IsModified ? "*" : "")}" +
                                $"{(TAE_EDITOR.IsReadOnlyFileMode ? " !READ ONLY!" : "")}" +
                                $" - DS Anim Studio {VERSION}";
                        else
                            Window.Title = $"DS Anim Studio {VERSION}";
                    }

                    prevFrameWasLoadingTaskRunning = IsLoadingTaskRunning;

                    IsFirstFrameActive = false;

                    GFX.World.Update(DELTA_UPDATE);

                    FmodManager.Update();

                    base.Update(gameTime);
                }
#if !DEBUG
            }
            catch (Exception ex)
            {
                if (!ErrorLog.HandleException(ex, "Fatal error encountered during update loop"))
                {
                    WinForm.Close();
                }
            }
#endif
            IsFirstUpdateLoop = false;
        }

        private void InitTonemapShader()
        {

        }

        protected override void Draw(GameTime gameTime)
        {
#if !DEBUG
            try
            {
#endif
                if ((ActiveHyst || IsLoadingTaskRunningHyst) || LoadingTaskMan.AnyTasksRunning())
                {
                    Input.Update(new Rectangle(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height).DpiScaled());

                    Colors.ReadColorsFromConfig();

                    DELTA_DRAW = (float)gameTime.ElapsedGameTime.TotalSeconds;// (float)(Math.Max(gameTime.ElapsedGameTime.TotalMilliseconds, 10) / 1000.0);

                    GFX.Device.Clear(Colors.MainColorBackground);

                ImGuiDraw.BeforeLayout(gameTime, 0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height, 0);
                
                OSD.Build(Main.DELTA_DRAW, 0, 0);
                ImGuiDebugDrawer.Begin();


                if (DbgMenuItem.MenuOpenState != DbgMenuOpenState.Open)
                    {
                        // Only update input if debug menu isnt fully open.
                        GFX.World.UpdateInput();
                    }

                    if (TAE_EDITOR.ModelViewerBounds.Width > 0 && TAE_EDITOR.ModelViewerBounds.Height > 0)
                    {
                        if (SceneRenderTarget == null)
                        {
                            RebuildRenderTarget();
                            if (TimeBeforeNextRenderTargetUpdate > 0)
                                TimeBeforeNextRenderTargetUpdate -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                        }
                        else if (RequestViewportRenderTargetResolutionChange)
                        {
                            RebuildRenderTarget();

                            if (TimeBeforeNextRenderTargetUpdate > 0)
                                TimeBeforeNextRenderTargetUpdate -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                        }


                    //GFX.Device.SetRenderTarget(UnusedRendertarget0);

                    //GFX.Device.Clear(Colors.MainColorViewportBackground);

                    //GFX.Device.Viewport = new Viewport(0, 0, UnusedRendertarget0.Width, UnusedRendertarget0.Height);

                    //GFX.LastViewport = new Viewport(TAE_EDITOR.ModelViewerBounds.DpiScaled());

                    //GFX.BeginDraw();
                    ////GFX.InitDepthStencil(writeDepth: false);
                    //DBG.DrawSkybox();

                    //GFX.Device.SetRenderTarget(null);


                    //GFX.Bokeh.Draw(SkyboxRenderTarget, GFX.BokehShapeHexagon, GFX.BokehRenderTarget,
                    //    GFX.BokehBrightness, GFX.BokehSize, GFX.BokehDownsize, GFX.BokehIsFullPrecision, GFX.BokehIsDynamicDownsize);

                    GFX.Device.SetRenderTarget(null);

                    //GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds.DpiScaled());
                    


                    


                    GFX.Device.SetRenderTarget(SceneRenderTarget);

                        GFX.Device.Clear(Colors.MainColorViewportBackground);

                        GFX.Device.Viewport = new Viewport(0, 0, SceneRenderTarget.Width, SceneRenderTarget.Height);

                        GFX.LastViewport = new Viewport(TAE_EDITOR.ModelViewerBounds.DpiScaled());

                    //GFX.SpriteBatchBegin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                    //GFX.SpriteBatch.Draw(SkyboxRenderTarget,
                    //new Rectangle(-SkyboxRenderTargetPadding, -SkyboxRenderTargetPadding,
                    //(TAE_EDITOR.ModelViewerBounds.Width + (SkyboxRenderTargetPadding * 2)) * GFX.EffectiveSSAA,
                    //(TAE_EDITOR.ModelViewerBounds.Height + (SkyboxRenderTargetPadding * 2)) * GFX.EffectiveSSAA), Color.White);
                    //GFX.SpriteBatchEnd();

                    GFX.Device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
                    //GFX.Device.Clear(ClearOptions.Stencil, Color.Transparent, 1, 0);
                    GFX.BeginDraw();
                    DBG.DrawSkybox();
                    //TaeInterop.TaeViewportDrawPre(gameTime);
                    GFX.DrawScene3D();

                    

                        //if (!DBG.DbgPrimXRay)
                        //    GFX.DrawSceneOver3D();

                        GFX.DrawPrimRespectDepth();

                        if (DBG.DbgPrimXRay)
                            GFX.Device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);

                        ImGuiDebugDrawer.ViewportOffset = TAE_EDITOR.ModelViewerBounds.DpiScaled().TopLeftCorner();

                        TAE_EDITOR?.Graph?.ViewportInteractor?.GeneralUpdate_BeforePrimsDraw();

                    GFX.DrawSceneOver3D();
                        ImGuiDebugDrawer.ViewportOffset = Vector2.Zero;

                    GFX.Device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);

                    GFX.DrawPrimDisrespectDepth();

                    GFX.Device.SetRenderTarget(null);

                        GFX.Device.Clear(Colors.MainColorBackground);

                        GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds.DpiScaled());

                        InitTonemapShader();
                        GFX.SpriteBatchBegin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

                        if (GFX.UseTonemap)
                        {
                            MainFlverTonemapShader.ScreenSize = new Vector2(
                                TAE_EDITOR.ModelViewerBounds.Width * Main.DPIX,
                                TAE_EDITOR.ModelViewerBounds.Height * Main.DPIY);
                            MainFlverTonemapShader.Effect.CurrentTechnique.Passes[0].Apply();
                        }



                    GFX.SpriteBatch.Draw(SceneRenderTarget,
                            new Rectangle(0, 0, TAE_EDITOR.ModelViewerBounds.Width, TAE_EDITOR.ModelViewerBounds.Height), Color.White);

                    if (RemoEventSim.CurrentFadeColor.HasValue)
                    {
                        GFX.SpriteBatchEnd();

                        GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds.DpiScaled());
                        GFX.SpriteBatchBegin(SpriteSortMode.BackToFront, BlendState.AlphaBlend);

                        GFX.SpriteBatch.Draw(TAE_EDITOR_BLANK_TEX,
                                new Rectangle(0, 0, TAE_EDITOR.ModelViewerBounds.Width,
                                TAE_EDITOR.ModelViewerBounds.Height), RemoEventSim.CurrentFadeColor.Value);
                    }

                    GFX.SpriteBatchEnd();

                        

                        //try
                        //{
                        //    using (var renderTarget3DScene = new RenderTarget2D(GFX.Device, TAE_EDITOR.ModelViewerBounds.Width * GFX.SSAA,
                        //   TAE_EDITOR.ModelViewerBounds.Height * GFX.SSAA, true, SurfaceFormat.Rgba1010102, DepthFormat.Depth24))
                        //    {
                        //        GFX.Device.SetRenderTarget(renderTarget3DScene);

                        //        GFX.Device.Clear(new Color(80, 80, 80, 255));

                        //        GFX.Device.Viewport = new Viewport(0, 0, TAE_EDITOR.ModelViewerBounds.Width * GFX.SSAA, TAE_EDITOR.ModelViewerBounds.Height * GFX.SSAA);
                        //        TaeInterop.TaeViewportDrawPre(gameTime);
                        //        GFX.DrawScene3D(gameTime);

                        //        GFX.Device.SetRenderTarget(null);

                        //        GFX.Device.Clear(new Color(80, 80, 80, 255));

                        //        GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds);

                        //        InitTonemapShader();
                        //        GFX.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                        //        //MainFlverTonemapShader.Effect.CurrentTechnique.Passes[0].Apply();
                        //        GFX.SpriteBatch.Draw(renderTarget3DScene,
                        //            new Rectangle(0, 0, TAE_EDITOR.ModelViewerBounds.Width, TAE_EDITOR.ModelViewerBounds.Height), Color.White);
                        //        GFX.SpriteBatch.End();
                        //    }
                        //}
                        //catch (SharpDX.SharpDXException ex)
                        //{
                        //    GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds);
                        //    GFX.Device.Clear(new Color(80, 80, 80, 255));

                        //    GFX.SpriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
                        //    //MainFlverTonemapShader.Effect.CurrentTechnique.Passes[0].Apply();
                        //    var errorStr = $"FAILED TO RENDER VIEWPORT AT {(Main.TAE_EDITOR.ModelViewerBounds.Width * GFX.SSAA)}x{(Main.TAE_EDITOR.ModelViewerBounds.Height * GFX.SSAA)} Resolution";
                        //    var errorStrPos = (Vector2.One * new Vector2(TAE_EDITOR.ModelViewerBounds.Width, TAE_EDITOR.ModelViewerBounds.Height) / 2.0f);

                        //    errorStrPos -= DBG.DEBUG_FONT.MeasureString(errorStr) / 2.0f;

                        //    GFX.SpriteBatch.DrawString(DBG.DEBUG_FONT, errorStr, errorStrPos - Vector2.One, Color.Black);
                        //    GFX.SpriteBatch.DrawString(DBG.DEBUG_FONT, errorStr, errorStrPos, Color.Red);
                        //    GFX.SpriteBatch.End();
                        //}

                    }



                    GFX.Device.Viewport = new Viewport(TAE_EDITOR.ModelViewerBounds.DpiScaled());
                    //DBG.DrawPrimitiveNames(gameTime);



                    //if (DBG.DbgPrimXRay)
                    //    GFX.DrawSceneOver3D();

                    GFX.DrawSceneGUI();



                    TAE_EDITOR?.Graph?.ViewportInteractor?.DrawDebugOverlay();

                    DrawMemoryUsage();

                    LoadingTaskMan.DrawAllTasks();


                    GFX.Device.Viewport = new Viewport(0, 0, (int)Math.Ceiling(Window.ClientBounds.Width / Main.DPIX), (int)Math.Ceiling(Window.ClientBounds.Height / Main.DPIY));

                    TAE_EDITOR.Rect = GetTaeEditorRect();

                    TAE_EDITOR.Draw(GraphicsDevice, TaeEditorSpriteBatch,
                        TAE_EDITOR_BLANK_TEX, TAE_EDITOR_FONT,
                        (float)gameTime.ElapsedGameTime.TotalSeconds, TAE_EDITOR_FONT_SMALL,
                        TAE_EDITOR_SCROLLVIEWER_ARROW);

                    if (IsLoadingTaskRunning)
                    {
                        GFX.Device.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
                        TAE_EDITOR.DrawDimmingRect(GraphicsDevice, TaeEditorSpriteBatch, TAE_EDITOR_BLANK_TEX);
                    }

                    GFX.Device.Viewport = new Viewport(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);

                ImGuiDebugDrawer.DrawTtest();

                ImGuiDebugDrawer.End();
                ImGuiDraw.AfterLayout(0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height, 0);

                //DrawImGui(gameTime, 0, 0, Window.ClientBounds.Width, Window.ClientBounds.Height);
            }
                //else
                //{
                //    // TESTING
                //    GFX.Device.Clear(Color.Fuchsia);
                //}
#if !DEBUG
            }
            catch (Exception ex)
            {
                if (!ErrorLog.HandleException(ex, "Fatal error ocurred during rendering"))
                {
                    Main.WinForm.Close();
                }
                GFX.Device.SetRenderTarget(null);
            }
#endif

            

            
        }
    }
}
