using Android.App;
using Android.Content.PM;
using Android.OS;

namespace FilesCombiner
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
          ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
          ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize |
          ConfigChanges.Density,
          LaunchMode = LaunchMode.SingleTop)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Request storage permissions for Android 10 and below
            if (Android.OS.Build.VERSION.SdkInt < Android.OS.BuildVersionCodes.Q)
            {
                if (CheckSelfPermission(Android.Manifest.Permission.ReadExternalStorage) != Permission.Granted ||
                    CheckSelfPermission(Android.Manifest.Permission.WriteExternalStorage) != Permission.Granted)
                {
                    RequestPermissions(new[]
                    {
                    Android.Manifest.Permission.ReadExternalStorage,
                    Android.Manifest.Permission.WriteExternalStorage
                }, 1);
                }
            }
        }
    }
}
