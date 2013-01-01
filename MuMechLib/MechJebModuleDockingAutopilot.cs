﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MechJebModuleDockingAutopilot : ComputerModule
    {
        public string status = "";

        public double approachSpeedMult = 1; // Approach speed will be approachSpeedMult * available thrust/mass on each axis.

        public double Kp = 0.2, Ki = 0, Kd = 0.02;

        public PIDController lateralPID;

        public MechJebModuleDockingAutopilot(MechJebCore core) : base(core)
        {
            lateralPID = new PIDController(Kp, Ki, Kd);
        }

        public override void OnModuleEnabled()
        {
            core.rcs.enabled = core.attitude.enabled = true;
            lateralPID = new PIDController(Kp, Ki, Kd);
        }

        public override void OnModuleDisabled()
        {
            core.rcs.enabled = core.attitude.enabled = false;
        }

        public override void Drive(FlightCtrlState s)
        {
            if (!Target.Exists())
            {
                enabled = false;
                return;
            }

            if (!vessel.ActionGroups[KSPActionGroup.RCS])
            {
                vessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
            }

            core.attitude.attitudeTo(Vector3d.back, AttitudeReference.TARGET_ORIENTATION, this);

            Vector3d targetVel = Target.Orbit().GetVel();

            Vector3d separation = Target.RelativePosition(vessel);

            Vector3d zAxis = (FlightGlobals.fetch.VesselTarget is ModuleDockingNode ? -Target.Transform().forward : Target.Transform().up); //the docking axis
            double zSep = -Vector3d.Dot(separation, zAxis); //positive if we are in front of the target, negative if behind
            Vector3d lateralSep = Vector3d.Exclude(zAxis, separation);

            double zApproachSpeed = vesselState.rcsThrustAvailable.GetMagnitude(-zAxis) * approachSpeedMult / vesselState.mass;
            double latApproachSpeed = vesselState.rcsThrustAvailable.GetMagnitude(-lateralSep) * approachSpeedMult / vesselState.mass;

            if (zSep < 0)  //we're behind the target
            {
                if (lateralSep.magnitude < 10) //and we'll hit the target if we back up
                {
                    core.rcs.SetTargetWorldVelocity(targetVel + zApproachSpeed * lateralSep.normalized); //move away from the docking axis
                    status = "Moving away from docking axis at " + zApproachSpeed.ToString("F2") + " m/s to avoid hitting target on backing up";
                }
                else
                {
                    core.rcs.SetTargetWorldVelocity(targetVel + Math.Max(-zApproachSpeed, zApproachSpeed * zSep / 50) * zAxis); //back up
                    status = "Backing up at " + Math.Max(-zApproachSpeed, zApproachSpeed * zSep / 50).ToString("F2") + " m/s to get on the correct side of the target to dock.";
                }
                lateralPID.Reset();
            }
            else //we're in front of the target
            {
                //move laterally toward the docking axis
                lateralPID.max = latApproachSpeed * lateralSep.magnitude / 200;
                lateralPID.min = -lateralPID.max;
                Vector3d lateralVelocityNeeded = -lateralSep.normalized * lateralPID.Compute(lateralSep.magnitude);
                if (lateralVelocityNeeded.magnitude > latApproachSpeed) lateralVelocityNeeded *= (latApproachSpeed / lateralVelocityNeeded.magnitude);

                double zVelocityNeeded = 0.1 + Math.Min(zApproachSpeed, zApproachSpeed * zSep / 200);

                if (lateralSep.magnitude > 0.2 && lateralSep.magnitude * 10 > zSep)
                {
                    //we're very far off the docking axis
                    if (zSep < lateralSep.magnitude)
                    {
                        //we're far off the docking axis, but our z separation is small. Back up to increase the z separation
                        zVelocityNeeded *= -1;
                        status = "Backing at " + zVelocityNeeded.ToString("F2") + " m/s up and moving toward docking axis.";
                    }
                    else
                    {
                        //we're not extremly close in z, so just stay at this z distance while we fix the lateral separation
                        zVelocityNeeded = 0;
                        status = "Holding still in Z and moving toward the docking axis at " + lateralVelocityNeeded.magnitude.ToString("F2") + " m/s.";
                    }
                }
                else
                {
                    if (zSep > 0.4)
                    {
                        //we're not extremely far off the docking axis. Approach the along z with a speed determined by our z separation
                        //but limited by how far we are off the axis
                        status = "Moving forward to dock at " + zVelocityNeeded.ToString("F2") + " m/s.";
                    }
                    else
                    {
                        // close enough, turn it off and let the magnetic dock work
                        enabled = false;
                    }
                }

                core.rcs.SetTargetWorldVelocity(targetVel + lateralVelocityNeeded + zVelocityNeeded * zAxis);
            }
        }
    }
}