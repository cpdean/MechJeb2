﻿using System;
using UnityEngine;

namespace MuMech
{
    public class OperationLan : Operation
    {
        public override string getName() { return "change longitude of ascending node";}

        [Persistent(pass = (int)Pass.Global)]
        public EditableDouble newLAN = 0;
        private TimeSelector timeSelector;

        public OperationLan ()
        {
            timeSelector = new TimeSelector(new TimeReference[] { TimeReference.APOAPSIS, TimeReference.PERIAPSIS, TimeReference.X_FROM_NOW });
        }

        public override void DoParametersGUI(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            timeSelector.DoChooseTimeGUI();
            GUILayout.Label("New Longitude of Ascending Node:");
            target.targetLongitude.DrawEditGUI(EditableAngle.Direction.EW);
        }

        public override ManeuverParameters MakeNodeImpl(Orbit o, double universalTime, MechJebModuleTargetController target)
        {
            if (o.inclination < 10)
                errorMessage = "Warning: orbital plane has a low inclination of " + o.inclination + "º (recommend > 10º) and so maneuver may not be accurate";

            double UT = timeSelector.ComputeManeuverTime(o, universalTime, target);

            var dV = OrbitalManeuverCalculator.DeltaVToShiftLAN(o, UT, target.targetLongitude);

            return new ManeuverParameters(dV, UT);
        }
    }
}

