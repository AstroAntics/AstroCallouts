//This is an example LSPDFR callout, in which
//the player responds to a report of a stolen SWAT car,
//and the criminals open fire when the player arrives.

//Initialize subdependencies.
using System;
using Rage;
using LSPDFR;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using LSPD_First_Response.Engine.Scripting.Entities;

namespace ExampleCallout
{
    [CalloutInfo("StolenSWATVan"), CalloutProbability.Medium)]
    public class StolenSWATVan : Callout
    {
        //Set up all the variables that we need for the callout...
        private Ped Suspect1; //Criminal 1
        private Ped Suspect2; //Criminal 2
        private Vehicle StolenVan; //The van
        private Vector3 CrimeLocation; //The location of the callout
        private Blip Blip1; //The first blip
        private Blip Blip2; //The second blip
        private Blip BlipVan; //The blip for the SWAT van
        private LHandle Pursuit; //A handler class for handling the van's pursuit.
        
        //A few booleans to handle the rest.
        private bool startedPursuit = false;
        private bool isDead = false;
        private bool vanDestroyed = false;

        //Before the callout starts, set all of this up.
        public override bool OnBeforeCalloutDisplayed()
        {
            //Generate a random spawn point within 150m of the player's position.
            CrimeLocation = World.GetNextPositionOnStreet(Game.LocalPlayer.Character.PositionAround(150f));           
            
            //Show the blip with a radius of 20m, then see if the player is further than 10m away.
            ShowCalloutAreaBlipBeforeAccepting(CrimeLocation, 20f);
            AddMinimumDistanceCheck(10f, CrimeLocation);

            //Create the actual callout, and set its position to the location of the crime.
            CalloutMessage = "Stolen SWAT Van";
            CalloutPosition = CrimeLocation;

            //Play the audio corresponding to the call at the player's position and let the player know about the details of the call.
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_GRAND_THEFT_AUTO IN_OR_ON_POSITION", CrimeLocation);
            UI.Notify("A ~r~ SWAT van ~w~ was reported stolen at this location. Respond to the call and confirm if it was stolen or not.");

            return base.OnBeforeCalloutDisplayed();
        }

        //Once the user accepts the callout, start this method.
        public override bool OnCalloutAccepted()
        {
            //Tell the user that they have accepted the callout.
            UI.Notify("Travel to the ~y~ location!");

            //Create the stolen SWAT van - a generic Police Riot, and make it permanent.
            StolenVan = new Vehicle("RIOT", CrimeLocation);
            StolenVan.IsPersistent = true;

            //Create a random driver and passenger for the stolen van, and make them permanent.
            Suspect1 = StolenVan.CreateRandomDriver();
            Suspect1.IsPersistent = true;
            Suspect1.BlockPermanentEvents = true;
            Suspect2 = StolenVan.CreateRandomPassenger();
            Suspect2.IsPersistent = true;
            Suspect2.BlockPermanentEvents = true;

            //Set up relationship groups.
            Suspect1.RelationshipGroup = "ROBBERS";
            Suspect2.RelationshipGroup = "ROBBERS";


            //Attach a blip to the van (no point in putting it on the suspects) and make it signify an enemy.
            VanBlip = StolenVan.AttachBlip();
            VanBlip.IsFriendly = false;
            return base.OnCalloutAccepted();
        }

        public override void Process()
        {
            //Call this first as a handler.
            base.Process();

            //Once the player gets close enough to the SWAT van (less than 10m), the callout begins.
            if (Game.LocalPlayer.Character.DistanceTo(StolenVan.position) < 10f)
            {
                //Pick a random number between 1 and 2 to determine what the suspects are doing. Very simple!
                Random SuspectBehavior = new Random(1,2);

                if (SuspectBehavior == 1)
                {
                    //Aggressive response.
                    if (Game.LocalPlayer.Character.DistanceTo(Suspect1.position) <= 10f) && (Game.LocalPlayer.Character.DistanceTo(Suspect2.position) <= 10f))
                    {
                        StartShooting();
                    }
                }

                else if (SuspectBehavior == 2)
                {
                    if (Game.LocalPlayer.Character.DistanceTo(Suspect1.position) <= 10f) && (Game.LocalPlayer.Character.DistanceTo(Suspect2.position) <= 10f))
                    //Passive response (suspects surrender)
                    Surrender();
                }

                //End the callout immediately if the van or the suspects don't exist.
                //This should never happen, which is why the EndWithError method exists.
                if (!StolenVan.Exists() || !Suspect1.Exists() || !Suspect2.Exists())
                {
                    EndWithError();
                    break;
                }
                
                 //If both suspects are arrested or dead, the call ends.
                 if ((Functions.IsPedArrested(Suspect1)) && (Functions.IsPedArrested(Suspect2)) || Suspect1.IsDead && Suspect2.IsDead)
                 {
                    Code4();
                    break;
                 }
                    //Move to Code 4 if the suspects escaped.
                 else if (Game.LocalPlayer.Character.DistanceTo(Suspect1.position) > 100f && Game.LocalPlayer.Character.DistanceTo(Suspect2.position) > 100f)
                 {
                    Code4();
                 }
           
                 else
                 {
                    UI.Notify("~y~ Move in ~w~ on the ~r~ suspects!");
                 }
            }  
        }
        
        //The suspects respond aggressively.
        public void StartShooting()
        {
            //Give suspects weapons and ammo
            Suspect1.Inventory.GiveNewWeapon("WEAPON_PISTOL", -1, true);
            Suspect1.Accuracy = 30;
            Suspect2.Inventory.GiveNewWeapon("WEAPON_ASSAULTRIFLE", -1, true);
            Suspect2.Accuracy = 35;
                        
            //Have the suspects exit the van and shout.
            Game.DisplaySubtitle("~r~ Suspect: ~w~ Fuck you!", 10);
            Suspect1.PlayAmbientSpeech("GENERIC_CURSE_HIGH");
            Suspect2.PlayAmbientSpeech("GENERIC_CURSE_HIGH");

            //Shoot at the player
            Suspect1.Tasks.FightAgainstClosestHatedTarget(50f);
            Suspect2.Tasks.FightAgainstClosestHatedTarget(50f);

            //Call in backup and inform the player!
            UI.Notify("The suspects are ~r~ resisting arrest! ~b~ Backup ~w~ has been summoned.");
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_SHOTS_FIRED IN_OR_ON_POSITION", Game.LocalPlayer.Character.Position);
            Functions.Game.RequestBackup
        }

        //The suspects peacefully surrender.
        public void Surrender()
        {
            //The suspects surrender.
            Game.DisplaySubtitle("~r~ Suspect: ~w~ Okay, okay! We give up. We give up.");
            UI.Notify("The suspects are ~y~ surrendering!");
            GameFiber.Wait(2000);
           
            //The suspects drop their weapons.
            NativeFunction.Natives.SET_PED_DROPS_WEAPON(Suspect1);
            Suspect1.Tasks.PutHandsUp(-1, Game.LocalPlayer.Character);
            NativeFunction.Natives.SET_PED_DROPS_WEAPON(Suspect2);
            Suspect2.Tasks.PutHandsUp(-1, Game.LocalPlayer.Character);
            StolenVan.Dismiss();
        }

        public void IsPlayerDead()
        {
            //If the player dies...
            if (Game.LocalPlayer.Character.IsDead)
            {
                GameFiber.Wait(1500);
                Functions.PlayScannerAudio("NOISE_SHORT OFFICER_NEEDS_IMMEDIATE_ASSISTANCE");
            }
        }

        //This will run if the callout was declared Code 4.
        public void Code4()
        {
            Functions.PlayScannerAudio("WE_ARE_CODE FOUR NO_FURTHER_UNITS_REQUIRED")
            Game.DisplayNotification("The callout has been resolved. We are ~y~ Code 4. ~w~ Good job, officer!");
            GameFiber.Wait(3000);
            End();
        }
        
        //Use this method to end everything. 
        //This is for a "normal" callout ending.
        public override void End()
        {

            //Delete everything, and end the callout.
            if (Suspect1.Exists()) {Suspect1.Delete()}
            if (Suspect2.Exists()) {Suspect2.Delete()}
            if (StolenVan.Exists()) {StolenVan.Delete()}
            if (Blip1.Exists()) {Blip1.Delete()}
            if (Blip2.Exists()) {Blip2.Delete()}
            if (BlipVan.Exists()) {BlipVan.Delete()}
            if (Pursuit.Exists()) {Pursuit.Delete()}
            StolenVan.Dismiss();
            return base.End();
        }
    
        //This method ends the callout if an error occurs. (Not normal.)
        public override void EndWithError()
        {
            UI.Notify("The callout ended because something went wrong." \n "If you keep seeing this message, please report it.");
            End();
        }
    
    }
}
