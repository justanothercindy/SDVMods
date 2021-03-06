﻿using System;
using Microsoft.Xna.Framework;
using StardewValley;
using TwilightShards.Stardew.Common;

namespace DynamicNightTime.Patches
{
    class GameClockPatch
    {
        public static void Postfix()
        {
            int sunriseTime = DynamicNightTime.GetSunrise().ReturnIntTime();
            int astronTime = DynamicNightTime.GetMorningAstroTwilight().ReturnIntTime();
            int sunsetTime = DynamicNightTime.GetSunset().ReturnIntTime();

            Color preSunrise = (Game1.isRaining ? Game1.ambientLight : Game1.eveningColor) * .15f;
            Color moonLight = new Color(0,0,0);

            if (DynamicNightTime.LunarDisturbancesLoaded && !Game1.isRaining && !Game1.isSnowing)
            {
                moonLight = DynamicNightTime.GetLunarLightDifference(Game1.timeOfDay);
            }

            if (Game1.timeOfDay <= astronTime)
            {
                Color oldLight = (Game1.eveningColor * .93f);

                if (DynamicNightTime.LunarDisturbancesLoaded && DynamicNightTime.MoonAPI.IsMoonUp(Game1.timeOfDay)) { 
                    oldLight.R = (byte)(oldLight.R - moonLight.R);
                    oldLight.G = (byte)(oldLight.G - moonLight.G);
                    oldLight.B = (byte)(oldLight.B - moonLight.B);
                }
                Game1.outdoorLight = oldLight;
            }

            else if (Game1.timeOfDay < sunriseTime && Game1.timeOfDay >= astronTime)
            {
                if (Game1.isRaining) { 
                    float minEff = SDVTime.MinutesBetweenTwoIntTimes(astronTime, Game1.timeOfDay) + (float)Math.Min(10.0, Game1.gameTimeInterval / 700);
                    float percentage = (minEff / SDVTime.MinutesBetweenTwoIntTimes(sunriseTime, astronTime));
                    Color destColor = new Color((byte)(237 - (58 * percentage)), (byte)(185 - (6 * percentage)), (byte)(74 + (105 * percentage)), (byte)(237 - (58 * percentage)));
                    Game1.outdoorLight = new Color((byte)(237 - (58 * percentage)), (byte)(185 - (6 * percentage)), (byte)(74 + (105 * percentage)), (byte)(237 - (58 * percentage)));
                }
                else
                { 
                    float minEff = SDVTime.MinutesBetweenTwoIntTimes(astronTime, Game1.timeOfDay) + (float)Math.Min(10.0, Game1.gameTimeInterval / 700);
                    float percentage = (minEff / SDVTime.MinutesBetweenTwoIntTimes(sunriseTime, astronTime));
                    //means delta r is -255, delta g is -159, delta b is +175 from evening to sunrise
                    //Normal sunrise is 0,96,175. Rainy sunrises are.. 0,50,148?
                    Color destColor = new Color((byte)(255 - (255*percentage)), (byte)(255 - (159*percentage)), (byte)(175 * percentage));
                    Game1.outdoorLight = destColor;
                }
            }
            else if (Game1.timeOfDay >= sunriseTime && Game1.timeOfDay <= Game1.getStartingToGetDarkTime())
            {
                if (Game1.isRaining) 
                    { 
                        Game1.outdoorLight = Game1.ambientLight * 0.3f;
                    }
                else 
                { 
                    //Goes from [0,96,175] to [0,5,1] to [0,98,193]
                    int solarNoon = DynamicNightTime.GetSolarNoon().ReturnIntTime();
                    if (Game1.timeOfDay < solarNoon)
                    {
                        float minEff = SDVTime.MinutesBetweenTwoIntTimes(Game1.timeOfDay, sunriseTime) + (float)Math.Min(10.0, Game1.gameTimeInterval / 700);
                        float percentage = (minEff / SDVTime.MinutesBetweenTwoIntTimes(sunriseTime, solarNoon));
                        Color destColor = new Color(0, (byte)(96 -(91*percentage)),(byte)(175 -(174*percentage)));
                        Game1.outdoorLight = destColor;
                    }
                    if (Game1.timeOfDay == solarNoon)
                    {
                        Color destColor = new Color(0,5,1);
                        Game1.outdoorLight = destColor;
                    }
                    if (Game1.timeOfDay > solarNoon)
                    {
                        float minEff = SDVTime.MinutesBetweenTwoIntTimes(Game1.timeOfDay, solarNoon) + (float)Math.Min(10.0, Game1.gameTimeInterval / 700);
                        float percentage = (minEff / SDVTime.MinutesBetweenTwoIntTimes(sunriseTime, solarNoon));
                        Color destColor = new Color(0, (byte)(5 + (93 * percentage)), (byte)(1 + (192 * percentage)));
                        Game1.outdoorLight = destColor;
                    }
                }
            }
            else if (Game1.timeOfDay >= Game1.getStartingToGetDarkTime())
            {
                //Goes from [0,98,193] to [255,255,0]. We should probably space this out so that civil is still fairly bright.
                int sunset = DynamicNightTime.GetSunset().ReturnIntTime();
                int astroTwilight = DynamicNightTime.GetAstroTwilight().ReturnIntTime();
                //Color navalColor = new Color(120,178,113);
                if (Game1.isRaining)
                {
                    if (Game1.timeOfDay >= Game1.getTrulyDarkTime())
                    {
                        float num = Math.Min(0.93f, (float)(0.75 + ((double)((int)((double)(Game1.timeOfDay - Game1.timeOfDay % 100) + (double)(Game1.timeOfDay % 100 / 10) * 16.6599998474121) - Game1.getTrulyDarkTime()) + (double)Game1.gameTimeInterval / 7000.0 * 16.6000003814697) * 0.000624999986030161));
                        Game1.outdoorLight = (Game1.isRaining ? Game1.ambientLight : Game1.eveningColor) * num;
                    }
                    else if (Game1.timeOfDay >= Game1.getStartingToGetDarkTime())
                    {
                        float num = Math.Min(0.93f, (float)(0.300000011920929 + ((double)((int)((double)(Game1.timeOfDay - Game1.timeOfDay % 100) + (double)(Game1.timeOfDay % 100 / 10) * 16.6599998474121) - Game1.getStartingToGetDarkTime()) + (double)Game1.gameTimeInterval / 7000.0 * 16.6000003814697) * 0.00224999990314245));
                        Game1.outdoorLight = (Game1.isRaining ? Game1.ambientLight : Game1.eveningColor) * num;
                    }
                }
                else
                { 
                    //civil
                    if (Game1.timeOfDay > sunset && Game1.timeOfDay < astroTwilight)
                    {
                        float minEff = SDVTime.MinutesBetweenTwoIntTimes(Game1.timeOfDay, sunset) + (float)Math.Min(10.0, Game1.gameTimeInterval / 700);
                        float percentage = (minEff / SDVTime.MinutesBetweenTwoIntTimes(sunset, astroTwilight));
                        Color destColor = new Color((byte)(0 + (227*percentage)), (byte)(98 + (111 * percentage)), (byte)(193 - (35 * percentage)), (byte)(255 - (17 * percentage)));
                        Game1.outdoorLight = destColor;
                        //[222,222,15]
                        if (Game1.timeOfDay > Game1.getModeratelyDarkTime() && (DynamicNightTime.LunarDisturbancesLoaded && DynamicNightTime.MoonAPI.IsMoonUp(Game1.timeOfDay)))
                        {
                            //start adding the moon in naval light
                            minEff = SDVTime.MinutesBetweenTwoIntTimes(Game1.timeOfDay, Game1.getModeratelyDarkTime()) + (float)Math.Min(10.0, Game1.gameTimeInterval / 700);
                            percentage = (minEff  / SDVTime.MinutesBetweenTwoIntTimes(Game1.getModeratelyDarkTime(), astroTwilight));

                            byte R = (byte)(destColor.R - (moonLight.R * percentage));
                            byte G = (byte)(destColor.G - (moonLight.G * percentage));
                            byte B = (byte)(destColor.B - (moonLight.B * percentage));

                            Game1.outdoorLight = new Color(R,G,B, Game1.outdoorLight.A);
                        }

                        //Game1.outdoorLight = SDVUtilities.SubtractTwoColors(destColor, moonLight);
                        //DynamicNightTime.Logger.Log($"The color is being set to {Game1.outdoorLight} from {destColor} with the moon being {moonLight}");
                    }

                    //astro
                    if (Game1.timeOfDay >= astroTwilight) { 
                        Game1.outdoorLight = (Game1.eveningColor * .93f);
                        if ((DynamicNightTime.LunarDisturbancesLoaded && DynamicNightTime.MoonAPI.IsMoonUp(Game1.timeOfDay))) { 
                            Game1.outdoorLight = SDVUtilities.SubtractTwoColors(Game1.outdoorLight, moonLight);
                        }
                    }
                }
            }
        }
    }
}
