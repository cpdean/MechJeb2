﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    class MechJebModuleManeuverPlanner : DisplayModule
    {
        public MechJebModuleManeuverPlanner(MechJebCore core) : base(core) { }

        enum Operation { CIRCULARIZE, ELLIPTICIZE, PERIAPSIS, APOAPSIS, INCLINATION, PLANE, TRANSFER, COURSE_CORRECTION, INTERPLANETARY_TRANSFER };
        static int numOperations = Enum.GetNames(typeof(Operation)).Length;
        Operation operation = Operation.CIRCULARIZE;

        enum Node { ASCENDING, DESCENDING };
        Node planeMatchNode;

        EditableDouble pe = new EditableDouble(0, 1000);
        EditableDouble ap = new EditableDouble(0, 1000);
        EditableDouble inc = new EditableDouble(0);
        EditableDouble lead = new EditableDouble(0);

        protected override void FlightWindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            GUILayout.Label("Insert a maneuver node to:");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("◅")) operation = (Operation)(((int)operation - 1 + numOperations) % numOperations);
            GUILayout.Label(operation.ToString());
            if (GUILayout.Button("▻")) operation = (Operation)(((int)operation + 1 + numOperations) % numOperations);
            GUILayout.EndHorizontal();

            switch (operation)
            {
                case Operation.CIRCULARIZE:
                    break;

                case Operation.ELLIPTICIZE:
                    GuiUtils.SimpleTextBox("Pe (km)", pe, 1000);
                    GuiUtils.SimpleTextBox("Ap (km)", ap, 1000);
                    break;

                case Operation.PERIAPSIS:
                    GuiUtils.SimpleTextBox("Pe (km)", pe, 1000);
                    break;

                case Operation.APOAPSIS:
                    GuiUtils.SimpleTextBox("Ap (km)", ap, 1000);
                    break;

                case Operation.INCLINATION:
                    GuiUtils.SimpleTextBox("Inc (deg)", inc, 1000);
                    break;

                case Operation.PLANE:
                    if (GUILayout.Button(planeMatchNode.ToString())) planeMatchNode = (Node)(((int)planeMatchNode + 1) % 2);
                    break;

                case Operation.TRANSFER:
                    break;

                case Operation.COURSE_CORRECTION:
                    break;

                case Operation.INTERPLANETARY_TRANSFER:
                    break;
            }

            GuiUtils.SimpleTextBox("In (seconds): ", lead, 1);
            double UT = vesselState.time + lead;


            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                double initialT = vesselState.time;
                double finalT = vesselState.time + 100;
                Vector3d initalRelPos = part.vessel.orbit.SwappedRelativePositionAtUT(initialT);
                Vector3d finalRelPos = part.vessel.orbit.SwappedRelativePositionAtUT(finalT);
                Vector3d knownInitialVel = part.vessel.orbit.SwappedOrbitalVelocityAtUT(initialT);
                Vector3d knownFinalVel = part.vessel.orbit.SwappedOrbitalVelocityAtUT(finalT);

                Vector3d computedInitialVel, computedFinalVel;
                LambertSolver.Solve(initalRelPos, initialT, finalRelPos, finalT, part.vessel.orbit.referenceBody, 0.01,
                    out computedInitialVel, out computedFinalVel);
                Debug.Log("--");
                Debug.Log("known initial velocity = " + knownInitialVel);
                Debug.Log("known final velocity = " + knownFinalVel);
            }


            if (Input.GetKeyDown(KeyCode.Alpha8))
            {
                Debug.Log("Maneuver nodes:");
                foreach (ManeuverNode mn in part.vessel.patchedConicSolver.maneuverNodes)
                {
                    Debug.Log(mn.ToString() + " at " + mn.UT + " - patch PeA = " + mn.patch.PeA + "; nextPatch PeA = " + (mn.nextPatch == null ? -66 : mn.nextPatch.PeA));
                }
            }


            if (GUILayout.Button("Go"))
            {
                Vector3d dV = Vector3d.zero;

                Orbit o = GetPatchAtUT(UT);
                double rad = o.referenceBody.Radius;

                Debug.Log("o.PeA = " + o.PeA);

                switch (operation)
                {
                    case Operation.CIRCULARIZE:
                        dV = OrbitalManeuverCalculator.DeltaVToCircularize(o, UT);
                        break;

                    case Operation.ELLIPTICIZE:
                        dV = OrbitalManeuverCalculator.DeltaVToEllipticize(o, UT, pe + rad, ap + rad);
                        break;

                    case Operation.PERIAPSIS:
                        dV = OrbitalManeuverCalculator.DeltaVToChangePeriapsis(o, UT, pe + rad);
                        break;

                    case Operation.APOAPSIS:
                        dV = OrbitalManeuverCalculator.DeltaVToChangeApoapsis(o, UT, ap + rad);
                        break;

                    case Operation.INCLINATION:
                        dV = OrbitalManeuverCalculator.DeltaVToChangeInclination(o, UT, inc);
                        break;

                    case Operation.PLANE:
                        if (planeMatchNode == Node.ASCENDING)
                        {
                            dV = OrbitalManeuverCalculator.DeltaVAndTimeToMatchPlanesAscending(o, FlightGlobals.fetch.VesselTarget.GetOrbit(), UT, out UT);
                        }
                        else
                        {
                            dV = OrbitalManeuverCalculator.DeltaVAndTimeToMatchPlanesDescending(o, FlightGlobals.fetch.VesselTarget.GetOrbit(), UT, out UT);
                        }
                        break;

                    case Operation.TRANSFER:
                        dV = OrbitalManeuverCalculator.DeltaVAndTimeForHohmannTransfer(o, FlightGlobals.fetch.VesselTarget.GetOrbit(), vesselState.time, out UT);
                        break;

                    case Operation.COURSE_CORRECTION:
                        dV = OrbitalManeuverCalculator.DeltaVForCourseCorrection(o, UT, FlightGlobals.fetch.VesselTarget.GetOrbit());
                        break;

                    case Operation.INTERPLANETARY_TRANSFER:
                        dV = OrbitalManeuverCalculator.DeltaVAndTimeForInterplanetaryTransferEjection(o, vesselState.time, FlightGlobals.fetch.VesselTarget.GetOrbit(), out UT);
                        break;
                }

                PlaceManeuverNode(o, dV, UT);
            }

            GUILayout.EndVertical();



            base.FlightWindowGUI(windowID);
        }


        public Orbit GetPatchAtUT(double UT)
        {
            IEnumerable<ManeuverNode> earlierNodes = part.vessel.patchedConicSolver.maneuverNodes.Where(n => n.UT < UT);
            Debug.Log("earlierNodes.Count() = " + earlierNodes.Count());
            Orbit o = part.vessel.orbit;
            if (earlierNodes.Count() > 0)
            {
                o = earlierNodes.OrderByDescending(n => n.UT).First().nextPatch;
            }
            Debug.Log("o.PeA = " + o.PeA);
            while (o.nextPatch != null && o.nextPatch.activePatch && o.nextPatch.StartUT < UT)
            {
                Debug.Log("next startUT = " + o.nextPatch.StartUT);
                o = o.nextPatch;
                Debug.Log("new PeA = " + o.PeA);
            }
            return o;
        }


        //input dV should be in world coordinates
        public void PlaceManeuverNode(Orbit patch, Vector3d dV, double UT)
        {
            //convert a dV in world coordinates into the coordinate system of the maneuver node,
            //which uses (x, y, z) = (radial+, normal-, prograde)
            Vector3d nodeDV = new Vector3d(Vector3d.Dot(patch.RadialPlus(UT), dV),
                                           Vector3d.Dot(-patch.NormalPlus(UT), dV),
                                           Vector3d.Dot(patch.Prograde(UT), dV));
            ManeuverNode mn = part.vessel.patchedConicSolver.AddManeuverNode(UT);
            mn.OnGizmoUpdated(nodeDV, UT);
        }

        public override GUILayoutOption[] FlightWindowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(300), GUILayout.Height(150) };
        }

        public override string GetName()
        {
            return "Maneuver Planner";
        }
    }
}
