﻿using Harmony;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using System;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley.Locations;
using TwilightShards.Common;
using TwilightShards.Stardew.Common;
using DynamicNightTime.Integrations;

namespace DynamicNightTime
{
    public class DynamicNightConfig
    {
        public double Latitude = 38.25;
        public bool SunsetTimesAreMinusThirty = true;
    }

    public class DynamicNightTime : Mod
    {
        public static double SunriseTemp = 2500;
        public static double SunsetTemp = 2700;
        public static double NoonTemp = 6300;
        public static double LateAfternoonTemp = 4250;
        public static double EarlyMorningTemp = 4250;
        public static DynamicNightConfig NightConfig;
        public static IMonitor Logger;
        public static bool LunarDisturbancesLoaded;
        public static ILunarDisturbancesAPI MoonAPI;
        private bool resetOnWakeup;
        private bool isNightOut;

        private static Type GetSDVType(string type)
        {
            const string prefix = "StardewValley.";

            return Type.GetType(prefix + type + ", Stardew Valley") ?? Type.GetType(prefix + type + ", StardewValley");
        }

        /// <summary> Provide an API interface </summary>
        private IDynamicNightAPI API;
        public override object GetApi()
        {
            return API ?? (API = new DynamicNightAPI());
        }

        public override void Entry(IModHelper helper)
        {
            isNightOut = false;
            Logger = Monitor;
            NightConfig = Helper.ReadConfig<DynamicNightConfig>();
            resetOnWakeup = false;

            //sanity check lat
            if (NightConfig.Latitude > 64)
                NightConfig.Latitude = 64;
            if (NightConfig.Latitude < -64)
                NightConfig.Latitude = -64;
    
            var harmony = HarmonyInstance.Create("koihimenakamura.dynamicnighttime");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            //patch getStartingToGetDarkTime
            MethodInfo setStartingToGetDarkTime = GetSDVType("Game1").GetMethods(BindingFlags.Static | BindingFlags.Public).ToList().Find(m => m.Name == "getStartingToGetDarkTime");
            MethodInfo postfix = typeof(Patches.GettingDarkPatch).GetMethods(BindingFlags.Static | BindingFlags.Public).ToList().Find(m => m.Name == "Postfix");
            Monitor.Log($"Postfixing {setStartingToGetDarkTime} with {postfix}", LogLevel.Trace);
            harmony.Patch(setStartingToGetDarkTime, null, new HarmonyMethod(postfix));

            //patch getTrulyDarkTime
            MethodInfo setTrulyDarkTime = GetSDVType("Game1").GetMethods(BindingFlags.Static | BindingFlags.Public).ToList().Find(m => m.Name == "getTrulyDarkTime");
            MethodInfo postfixDark = typeof(Patches.GetFullyDarkPatch).GetMethods(BindingFlags.Static | BindingFlags.Public).ToList().Find(m => m.Name == "Postfix");
            Monitor.Log($"Postfixing {setTrulyDarkTime} with {postfixDark}", LogLevel.Trace);
            harmony.Patch(setTrulyDarkTime, null, new HarmonyMethod(postfixDark));

            //patch isDarkOut
            MethodInfo isDarkOut = GetSDVType("Game1").GetMethods(BindingFlags.Static | BindingFlags.Public).ToList().Find(m => m.Name == "isDarkOut");
            MethodInfo postfixIsDarkOut = typeof(Patches.IsDarkOutPatch).GetMethods(BindingFlags.Static | BindingFlags.Public).ToList().Find(m => m.Name == "Postfix");
            Monitor.Log($"Postfixing {isDarkOut} with {postfixIsDarkOut}", LogLevel.Trace);
            harmony.Patch(isDarkOut, null, new HarmonyMethod(postfixIsDarkOut));

            //patch UpdateGameClock
            MethodInfo UpdateGameClock = helper.Reflection.GetMethod(GetSDVType("Game1"), "UpdateGameClock").MethodInfo;
            MethodInfo postfixClock = helper.Reflection.GetMethod(typeof(Patches.GameClockPatch), "Postfix").MethodInfo;
            Monitor.Log($"Postfixing {UpdateGameClock} with {postfixClock}", LogLevel.Trace);
            harmony.Patch(UpdateGameClock, null, new HarmonyMethod(postfixClock));

            GameEvents.FirstUpdateTick += GameEvents_FirstUpdateTick;
            TimeEvents.AfterDayStarted += HandleNewDay;
            SaveEvents.AfterReturnToTitle += HandleReturn;
            TimeEvents.TimeOfDayChanged += HandleTimeChanges;
            
            Helper.ConsoleCommands.Add("debug_cycleinfo", "Outputs the cycle information", OutputInformation);
            Helper.ConsoleCommands.Add("debug_outdoorlight", "Outputs the outdoor light information", OutputLight);
            Helper.ConsoleCommands.Add("debug_setlatitude", "Sets Latitude", SetLatitude);
            
        }

        private void GameEvents_FirstUpdateTick(object sender, EventArgs e)
        {
            //testing for ZA MOON, YOUR HIGHNESS.
            MoonAPI = SDVUtilities.GetModApi<ILunarDisturbancesAPI>(Monitor, Helper, "KoihimeNakamura.LunarDisturbances", "1.0.7");

            if (MoonAPI != null)
                LunarDisturbancesLoaded = true;
        }

        private void SetLatitude(string arg1, string[] arg2)
        {
           if (arg2.Length > 0)
            {
                NightConfig.Latitude = Convert.ToDouble(arg2[0]);
            }
        }

        private void HandleReturn(object sender, EventArgs e)
        {
            resetOnWakeup = false;
        }

        private void HandleNewDay(object sender, EventArgs e)
        {
            if (Game1.isDarkOut() && !Game1.currentLocation.IsOutdoors && Game1.currentLocation is DecoratableLocation loc && !resetOnWakeup)
            {
                //we need to handle the spouse's room
                if (loc is FarmHouse)
                {
                    if (Game1.timeOfDay < GetSunriseTime())
                        Game1.currentLocation.switchOutNightTiles();
                }
            }
            resetOnWakeup = false;
        }

        private void HandleTimeChanges(object sender, EventArgsIntChanged e)
        {
            //handle ambient light changes.
            if (!Game1.currentLocation.IsOutdoors && Game1.currentLocation is DecoratableLocation locB)
            {
                Game1.ambientLight = Game1.isDarkOut() || locB.LightLevel > 0.0 ? new Color(180, 180, 0) : Color.White;
            }
            
            //handle the game being bad at night->day :|
            if (Game1.timeOfDay < GetSunriseTime())
            {
                Game1.currentLocation.switchOutNightTiles();
                isNightOut = true;
            }

            if (Game1.timeOfDay >= GetSunriseTime() && isNightOut)
            {
                Game1.currentLocation.addLightGlows();
                isNightOut = false;
            }
        }

        private void OutputLight(string arg1, string[] arg2)
        {
            Monitor.Log($"The outdoor light is {Game1.outdoorLight.ToString()}. The ambient light is {Game1.ambientLight.ToString()}");
        }

        private void OutputInformation(string arg1, string[] arg2)
        {
            Monitor.Log($"Sunrise : {GetSunrise().ToString()}, Sunset: {GetSunset().ToString()}. Solar Noon {GetSolarNoon().ToString()}");
            Monitor.Log($"Early Morning ends at {GetEndOfEarlyMorning().ToString()}, Late Afternoon begins at {GetBeginningOfLateAfternoon().ToString()}");
            Monitor.Log($"Morning Twilight: {GetMorningAstroTwilight().ToString()}, Evening Twilight: {GetAstroTwilight().ToString()}");
            Monitor.Log($"Game Interval Time is {Game1.gameTimeInterval}");
        }

        public static Color GetLunarLightDifference(int timeOfDay)
        {
          //full moon +22
          //first/third quarter + 11
          //gibbeous + 17
          //crescent +5

          switch (MoonAPI.GetCurrentMoonPhase())
          {
                case "Third Quarter":
                case "First Quarter":
                    return new Color(11,11,0);
                case "Full Moon":
                    return new Color(22,22,0);
                case "Waning Gibbeous":
                case "Waxing Gibbeous":
                    return new Color(17,17,0);
                case "Waning Crescent":
                case "Waxing Crescent":
                    return new Color(5,5,0);
                default:
                    return new Color(0,0,0);
          }
        }

        public static int GetSunriseTime() => GetSunrise().ReturnIntTime();
        public static SDVTime GetMorningAstroTwilight() => GetTimeAtHourAngle(-0.314159265);
        public static SDVTime GetAstroTwilight() => GetTimeAtHourAngle(-0.314159265, false);
        public static SDVTime GetMorningNavalTwilight() => GetTimeAtHourAngle(-0.20944);
        public static SDVTime GetNavalTwilight() => GetTimeAtHourAngle(-0.20944, false);
        public static SDVTime GetCivilTwilight() => GetTimeAtHourAngle(-0.104719755, false);
        public static SDVTime GetMorningCivilTwilight() => GetTimeAtHourAngle(-0.104719755);
        public static SDVTime GetSunrise() => GetTimeAtHourAngle(0.01163611);
        public static SDVTime GetSunset()
        {
            SDVTime s =  GetTimeAtHourAngle(0.01163611, false);
            if (NightConfig.SunsetTimesAreMinusThirty) s.AddTime(-30);
            return s;
        }

        protected internal static SDVTime GetSolarNoon()
        {
            var date = SDate.Now();
            int dayOfYear = date.DaysSinceStart % 112;
            double lat = GeneralFunctions.DegreeToRadians(NightConfig.Latitude);

            double solarDeclination = .40927971 * Math.Sin((2 * Math.PI / 112) * (dayOfYear - 1));
            double noon = 720 - 10 * Math.Sin(4 * (Math.PI / 112) * (dayOfYear - 1)) + 8 * Math.Sin(2 * (Math.PI / 112) * dayOfYear);
            
            int noonTime = (int)Math.Floor(noon);
            int hr = (int)Math.Floor(noonTime / 60.0);
            SDVTime calcTime = new SDVTime(hr, noonTime - (hr * 60));
            calcTime.ClampToTenMinutes();
            return calcTime;
        }

        protected internal static SDVTime GetEndOfEarlyMorning()
        {
            SDVTime Noon = GetSolarNoon();
            SDVTime Sunrise = GetSunrise();

            int numMinutes = Noon.GetNumberOfMinutesFromMidnight() - Sunrise.GetNumberOfMinutesFromMidnight();
            int endOfEarlyMorning = (int)Math.Floor(numMinutes * .38);

            SDVTime EndOfEarlyMorning = new SDVTime(Sunrise);
            EndOfEarlyMorning.AddTime(endOfEarlyMorning);
            EndOfEarlyMorning.ClampToTenMinutes();

            return EndOfEarlyMorning;
        }

        protected internal static SDVTime GetBeginningOfLateAfternoon()
        {
            SDVTime Noon = GetSolarNoon();
            SDVTime Sunset = GetSunset();

            int numMinutes = Sunset.GetNumberOfMinutesFromMidnight() - Noon.GetNumberOfMinutesFromMidnight();
            int lateAfternoon = (int)Math.Floor(numMinutes * .62);

            SDVTime LateAfternoon  = new SDVTime(Noon);
            LateAfternoon.AddTime(lateAfternoon);
            LateAfternoon.ClampToTenMinutes();

            return LateAfternoon;
        }

        protected internal static SDVTime GetTimeAtHourAngle(double angle, bool morning = true)
        {
            var date = SDate.Now();
            int dayOfYear = date.DaysSinceStart % 112;
            double lat = GeneralFunctions.DegreeToRadians(NightConfig.Latitude);

            double solarDeclination = .40927971 * Math.Sin((2 * Math.PI / 112) * (dayOfYear - 1));
            double noon = 720 - 10 * Math.Sin(4 * (Math.PI / 112) * (dayOfYear - 1)) + 8 * Math.Sin(2 * (Math.PI / 112) * dayOfYear);
            double astroHA = Math.Acos((Math.Sin(angle) - Math.Sin(lat) * Math.Sin(solarDeclination)) / (Math.Cos(lat) * Math.Cos(solarDeclination)));
            double minHA = (astroHA / (2 * Math.PI)) * 1440;
            int astroTwN = 0;

            if (!morning)
                astroTwN = (int)Math.Floor(noon + minHA);
            else
                astroTwN = (int)Math.Floor(noon - minHA);

            //Conv to an SDV compat time, then clamp it.
            int hr = (int)Math.Floor(astroTwN / 60.0);
            int min = astroTwN - (hr * 60);
            SDVTime calcTime = new SDVTime(hr, min);
            calcTime.ClampToTenMinutes();
            return calcTime;
        }
    }
}
