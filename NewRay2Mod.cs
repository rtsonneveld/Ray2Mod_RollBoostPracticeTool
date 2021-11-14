using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Ray2Mod;
using Ray2Mod.Components;
using Ray2Mod.Components.Text;
using Ray2Mod.Components.Types;
using Ray2Mod.Game;
using Ray2Mod.Game.Functions;
using Ray2Mod.Game.Structs.Material;
using Ray2Mod.Game.Structs.MathStructs;
using Ray2Mod.Game.Types;
using Ray2Mod.Utils;

/* To begin modding Rayman 2, you have to add Ray2Mod.dll as a reference to this project:
 * 1. Right click References in the Solution Explorer
 * 2. Click Browse and locate Ray2Mod.dll 
 * 3. Press OK to add the reference
 * 
 * Now you can run your mod by building this project and dragging the exported DLL onto ModRunner.exe
 * To automatically start the ModRunner when clicking Start, configure the following:
 * 1. Click Project -> <YourProject> Properties...
 * 2. Open the Debug configuration on the left
 * 3. As start action, select "Start external program" and Browse for ModRunner.exe
 * 4. Under start options, set the command line arguments to <YourProject>.dll (for example Ray2Mod_RollBoostPracticeTool.dll)
 * 
 * Now the ModRunner will start and inject your mod whenever you click start.
 */

namespace Ray2Mod_RollBoostPracticeTool {

    public unsafe class NewRay2Mod : IMod {

        RemoteInterface ri;

        private World world;

        private int groundTimer;
        private int hoverStartTime;
        private int hoverEndTime;
        private bool rollBoostActive;
        private bool showParticles;
        
        private static float TOLERANCE = 0.05f;

        enum State
        {
            Ground,
            Jumping,
            Hover,
            AfterHover
        }

        private State state;
        private Texture sparkTextureYellow;
        private Texture sparkTextureRed;
        private VisualMaterial sparkMatRed;
        private VisualMaterial sparkMatYellow;

        private List<float> speedsXY = new List<float>();
        private List<float> speedsXYZ = new List<float>();
        private int averageDuration = 60;
        
        private float averageSpeedXY = 0;
        private float averageSpeedXYZ = 0;

        private Random rand;

        void Loop()
        {
            var spos = world.GetSuperObjectsWithNames(world.ActiveDynamicWorld);
            if (spos.ContainsKey("Rayman")) {
                var raymanSpo = spos["Rayman"];
                var gravity = raymanSpo.StructPtr->PersoData->dynam->DynamicsBase->DynamicsBlockBase.m_xGravity;

                var dsgVars = raymanSpo.StructPtr->PersoData->GetDsgVarList();

                bool onGround = Math.Abs(gravity - 9.81f) < TOLERANCE;
                bool hovering = *((byte*)dsgVars[9].valuePtrCurrent) == 15;

                rollBoostActive = *((byte*)dsgVars[33].valuePtrCurrent) == 1;

                var speed = raymanSpo.StructPtr->PersoData->dynam->DynamicsBase->DynamicsBlockBase.m_pstReport->
                    stAbsoluteCurrSpeed.stLinear;
                speedsXY.Add((float)Math.Sqrt(speed.x*speed.x + speed.y*speed.y)); // XY speed
                speedsXYZ.Add((float)Math.Sqrt(speed.x*speed.x + speed.y*speed.y + speed.z*speed.z)); // XYZ speed
                while (speedsXY.Count > averageDuration) {
                    speedsXY.RemoveAt(0);
                }
                while (speedsXYZ.Count > averageDuration) {
                    speedsXYZ.RemoveAt(0);
                }

                averageSpeedXY = speedsXY.Sum() / speedsXY.Count;
                averageSpeedXYZ = speedsXYZ.Sum() / speedsXYZ.Count;

                switch (state) {
                    case State.Ground:

                        groundTimer++;

                        if (!onGround) {
                            state = State.Jumping;
                            hoverStartTime = 0;
                            hoverEndTime = 0;
                        }
                        break;
                    case State.Jumping:

                        hoverStartTime++;

                        if (hovering) {
                            state = State.Hover;
                        } else if (onGround) {
                            groundTimer = 0;
                            state = State.Ground;
                        }
                        break;
                    case State.Hover:

                        hoverEndTime++;

                        if (onGround) {
                            groundTimer = 0;
                            state = State.Ground;
                        } else if (!hovering) {
                            state = State.AfterHover;
                        }

                        break;
                    case State.AfterHover:
                        if (onGround) {
                            groundTimer = 0;
                            state = State.Ground;
                        }
                        break;
                }

                if (rollBoostActive && showParticles) {
                    var raymanSpoMatrix = raymanSpo.StructPtr->matrixPtr->TransformationMatrix;
                    var pos = raymanSpoMatrix.Translation;

                    var ptr = new StructPtr<Vector3>(new Vector3(pos.X, pos.Y, pos.Z));
                    var ptr2 = new StructPtr<Vector3>(new Vector3(0,0,0));

                    var vmt = rand.Next() % 2 == 0 ? sparkMatRed : sparkMatYellow;

                    GfxFunctions.VAddParticle.Call(9, new IntPtr(ptr), new IntPtr(ptr2), vmt.ToUnmanaged(), 0.05f);
                }
            }
        }

        unsafe void IMod.Run(RemoteInterface remoteInterface)
        {
            ri = remoteInterface;
            world = new World();

            ri.Log("New Project, Hello World!");

            rand = new Random();

            List<Texture> textures = TextureLoader.GetTextures();

            foreach (Texture t in textures) {
                ri.Log($"Texture ptr 0x{(int)t.TexData:X} {t.Name}");
            }

            sparkTextureYellow = textures.FirstOrDefault(t => t.Name == "effets_speciaux\\etincelle_doree_ad.tga");
            sparkTextureRed = textures.FirstOrDefault(t => t.Name == "effets_speciaux\\etincelle_rouge_ad.tga");

            sparkMatRed = new VisualMaterial()
            {
                flags = 1306265599,
                ambientCoef = new Vector4(0.3f, 0.3f, 0.3f, 0.3f),
                diffuseCoef = new Vector4(1.0f, 1.0f, 1.0f, 1.0f),
                off_texture = sparkTextureRed.TexData,
            };

            sparkMatYellow = new VisualMaterial()
            {
                flags = 1306265599,
                ambientCoef = new Vector4(0.3f, 0.3f, 0.3f, 0.3f),
                diffuseCoef= new Vector4(1.0f,1.0f,1.0f,1.0f),
                off_texture = sparkTextureYellow.TexData,
            };

            GlobalActions.PreEngine += Loop;

            GlobalInput.Actions['p'] = () =>
            {
                showParticles = !showParticles;
            };

            GlobalInput.Actions['o'] = () =>
            {
                averageDuration += 60;
                if (averageDuration > 600) {
                    averageDuration = 60;
                }
            };

            TextOverlay showParticlesText = new TextOverlay((previousText) =>
            {
                return $"Press P to {(showParticles ? "disable" : "enable")} particles";
            }, 6, 5, 650).Show();
            TextOverlay showRollingAverageText = new TextOverlay((previousText) =>
            {
                return $"Press O to change rolling average duration ({averageDuration})";
            }, 6, 5, 675).Show();

            TextOverlay rollBoostActiveText = new TextOverlay((previousText) =>
            {
                return $"Roll Boost Active: {(rollBoostActive?"true":"false")}";
            }, 10, 5, 700).Show();

            TextOverlay groundTimerText = new TextOverlay((previousText) =>
            {
                return $"Time on Ground: {groundTimer} frames";
            }, 10, 5, 740).Show();

            TextOverlay hoverStartTimeText = new TextOverlay((previousText) =>
            {
                return $"Time before hover start: {hoverStartTime} frames";
            }, 10, 5, 780).Show();

            TextOverlay hoverEndTimeText = new TextOverlay((previousText) =>
            {
                return $"Time before hover end: {hoverEndTime} frames";
            }, 10, 5, 820).Show();

            TextOverlay averageSpeedText = new TextOverlay((previousText) =>
            {
                return $"Average XY Speed: {averageSpeedXY:F2} over {speedsXY.Count} frames";
            }, 10, 5, 860).Show();

            TextOverlay averageSpeedXYZText = new TextOverlay((previousText) =>
            {
                return $"Average XYZ Speed: {averageSpeedXYZ:F2} over {speedsXYZ.Count} frames";
            }, 10, 5, 900).Show();
        }
    }
}
