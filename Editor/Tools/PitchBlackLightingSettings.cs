namespace P3k.UnityEditorTools.Tools
{
   using System.Linq;

   using UnityEditor.SceneManagement;

   using UnityEngine;
   using UnityEngine.Rendering;

   public enum EnvLightingSource { Color, Skybox, Gradient }

   public enum EnvReflectionSource { Custom, Skybox }

   public struct PitchBlackLightingSettings
   {
      public Color AmbientColor;

      public Color AmbientEquator;

      public Color AmbientGround;

      public Color AmbientSky;

      public bool ClearSkyboxWhenNotSky;

      public Cubemap CustomReflection;

      public bool DisableBakedGI;

      public bool DisableFog;

      public bool DisableRealtimeGI;

      public EnvLightingSource EnvLighting;

      public int ReflectionBounces;

      public float ReflectionIntensity;

      public EnvReflectionSource ReflectionSource;

      public Material SkyboxMaterial;

      public Color SubtractiveShadowColor;
   }

   public static class PitchBlackLightingTool
   {
      public static void Apply(PitchBlackLightingSettings s, bool saveActiveScene)
      {
         switch (s.EnvLighting)
         {
            case EnvLightingSource.Color:
               RenderSettings.ambientMode = AmbientMode.Flat;
               RenderSettings.ambientLight = s.AmbientColor;
               if (s.ClearSkyboxWhenNotSky)
               {
                  RenderSettings.skybox = null;
               }

               break;

            case EnvLightingSource.Gradient:
               RenderSettings.ambientMode = AmbientMode.Trilight;
               RenderSettings.ambientSkyColor = s.AmbientSky;
               RenderSettings.ambientEquatorColor = s.AmbientEquator;
               RenderSettings.ambientGroundColor = s.AmbientGround;
               if (s.ClearSkyboxWhenNotSky)
               {
                  RenderSettings.skybox = null;
               }

               break;

            case EnvLightingSource.Skybox:
               RenderSettings.ambientMode = AmbientMode.Skybox;
               if (s.SkyboxMaterial != null)
               {
                  RenderSettings.skybox = s.SkyboxMaterial;
               }

               break;
         }

         RenderSettings.subtractiveShadowColor = s.SubtractiveShadowColor;

         if (s.ReflectionSource == EnvReflectionSource.Skybox)
         {
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.customReflectionTexture = null;
         }
         else
         {
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = s.CustomReflection;
         }

         RenderSettings.reflectionIntensity = Mathf.Clamp01(s.ReflectionIntensity);
         RenderSettings.reflectionBounces = Mathf.Clamp(s.ReflectionBounces, 1, 5);

         if (s.DisableFog)
         {
            RenderSettings.fog = false;
         }

         //LightingSettings.realtimeGI = !s.DisableRealtimeGI;
         //LightingSettings.bakedGI = !s.DisableBakedGI;

         if (s.EnvLighting == EnvLightingSource.Skybox && Mathf.Approximately(s.ReflectionIntensity, 0f))
         {
            RenderSettings.ambientIntensity = 0f;
         }

         if (saveActiveScene)
         {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid())
            {
               EditorSceneManager.MarkSceneDirty(scene);
               EditorSceneManager.SaveScene(scene);
            }
         }
      }
   }
}