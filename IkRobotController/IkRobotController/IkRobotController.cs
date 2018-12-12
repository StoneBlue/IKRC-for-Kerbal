﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using KSP.IO;
using System.Globalization;

// FELADATOK:
// - Plugin run only flight mode ???
// - Plugin start only if push button and exist on arm
// - Check module part is PDGF ???
// - RobotArm with dynamic baseposition ??? - dynamic dumping servo structure ???
// - Initialize from notbase position (any theta != 0)
// - A-Lee is ModulePart ???
// - Separate ServoStructure data from all servo data
// - Save/Load ServoStructure To/From config file in EditorMode
// - Get Axis information from arm structure

/*
 * CheckServosZeroState()
 * {
 * 	StatesToThetas()
 * 	...
 * 	..
 * 	.
 * }
 * 
 * Start()
 * {
 * 	if(!InitIRWrapper())
 * 		return;
 * 	if(!CheckExistArm())
 * 		return;
 * 	if(!GetArmServos())
 * 		return;
 * 	ServosIsBaseInited = true;
 * 	if(!IsLoadedFkParams)
 *  {
 * 		if(!CheckServosZeroState())
 * 			return;
 * 		if(!DumpFkParams())
 * 			return;
 *  }
 *	if(!GetFkParamsFromBuffer())
 *		return;
 *	IsInitedModule = true;
 * }
 * 
 * Update()
 * {
 * 	if(ServosIsBaseInited && !DumpEventActive)
 * 		DumpEventActive = true;
 * 	...
 * 	..
 * 	.
 * }
 * 
 * DumpEvent()
 * {
 * 	...
 * 	..
 * 	.
 * 	if(GetFkParamsFromBuffer())
 * 		IsInitedModule = true;
 * }
 *
*/



namespace IkRobotController
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class IkRobotController : PartModule
    {
        #region module variable of part.cfg
        [KSPField(isPersistant = false)]
        public string controlButtonUpDownColor = "";

        [KSPField(isPersistant = false)]
        public string controlButtonLeftRightColor = "";

        [KSPField(isPersistant = false)]
        public string controlButtonForwardBackwardColor = "";
        #endregion module variable of part.cfg

        private bool buttonIsColored = false;
        private Color colorControlButtonUpDown;
        private Color colorControlButtonLeftRight;
        private Color colorControlButtonForwardBackward;

        private static int onSaveCounter = 0;
        private static int onLoadCounter = 0;

        protected static int NextWindowID = 110;
        protected int WindowID = 0;
        protected int MsgWindowID = 0;

        public bool IsInitedModule = false;
        public bool IsInitedModule1st = true;
        private bool IsLoadedFkParams = false;
        private bool ServosIsBaseInited = false;
        private bool DumpEventIsActive = false;
        private bool IsSaveServoStructure = false;

        private string msgWindowTitle = "";
        private string msgText = "";

        private GUIStyle redStyle = null;
        private GUIStyle greenStyle = null;
        private GUIStyle blueStyle = null;

        private Rect windowRectangle;
        private Rect msgWindowRectangle;
        private Vector2 windowPosition = new Vector2(10, 10);
        private Vector2 windowSize = new Vector2(310, 600);
        private Vector2 msgWindowPosition = new Vector2(100, 100);
        private Vector2 msgWindowSize = new Vector2(200, 100);
        private Rect inputRect;
        private ConfigNode nodeInner;
        private bool IK_active = false;
        private bool IKButton_active = false;
        private bool servoTransformHas = false;
        private bool targetIconHas = false;
        private bool zeroThetaIterate = false;
        private bool clearThetasBool = false;
        private bool baseStateBool = false;
        private bool actualStateBool = false;
        private bool pdgfStateBool = false;
        private bool veeStateBool = false;
        private bool refreshPDGFBool = false;
        private bool ircWindowActive = false;
        private bool msgWindowActive = false;
        private bool emergencyServoStop = false;
        private float Yoffset = 0;
        private float servoSpeed = 0.25f;
        private float transStep = 0.25f;
        private float rotStep = 0.25f;

        private float ArmMaxLength = 8.0f;

        private string iterateString = "500";
        private string forceDistStringZ = "0.6";
        private string forceRollStringZ = "0.0";
        private string forceDistStringX = "0.0";
        private string forceRollStringX = "0.0";
        private string forceDistStringY = "0.0";
        private string forceRollStringY = "0.0";
        private string samplingAngleString = "0.02";
        private string DistanceThresholdString = "0.01";
        private string AngleThresholdString = "0.5";
        private string MaxPosErrString = "10";
        private string MaxOriErrString = "180";
        private string[] thetaString = { "0", "0", "0", "0", "0", "0", "0" };
        private string ArmMaxLengthString = "8.0";

        private Vector2 trButtonsOrigin = new Vector2(10, 35);
        private Vector2 closeWindowButtonPosition = new Vector2(290, 3);
        private Rect diagramRectangle;
        private List<float> iterateData;
        private List<float> sampleAngleData;
        private List<List<float>> data;

        private bool WindowIsExtended = false;

        public class VectorSM
        {
            public Vector3 Translation;
            public Quaternion Rotation;

            public VectorSM()
            {
                Translation = new Vector3();
                Rotation = Quaternion.identity;
            }
        }

        public class LocationRotationDiff
        {
            public float LocationDiff;
            public float RotationDiff;

            public LocationRotationDiff()
            {
            }
        }

        public class Buttons
        {
            public Vector3 Translation;
            public Quaternion Rotation;

            public Buttons()
            {
                Translation = new Vector3();
                Rotation = Quaternion.identity;
            }
        }

        public class FKparams
        {
            public Quaternion PartRotation;
            public Vector3 ParentOffset;
            public Vector3 Axis;
            //public Vector3 Position;
            public Quaternion Rotation;

            public FKparams()
            {
            }

            public FKparams Clone()
            {
                FKparams destination = new FKparams();

                destination.PartRotation.x = this.PartRotation.x;
                destination.PartRotation.y = this.PartRotation.y;
                destination.PartRotation.z = this.PartRotation.z;
                destination.PartRotation.w = this.PartRotation.w;
                destination.ParentOffset.x = this.ParentOffset.x;
                destination.ParentOffset.y = this.ParentOffset.y;
                destination.ParentOffset.z = this.ParentOffset.z;
                destination.Axis.x = this.Axis.x;
                destination.Axis.y = this.Axis.y;
                destination.Axis.z = this.Axis.z;
                destination.Rotation.x = this.Rotation.x;
                destination.Rotation.y = this.Rotation.y;
                destination.Rotation.z = this.Rotation.z;
                destination.Rotation.w = this.Rotation.w;

                return destination;
            }

            public void Copy(FKparams from)
            {
                this.PartRotation.x = from.PartRotation.x;
                this.PartRotation.y = from.PartRotation.y;
                this.PartRotation.z = from.PartRotation.z;
                this.PartRotation.w = from.PartRotation.w;
                this.ParentOffset.x = from.ParentOffset.x;
                this.ParentOffset.y = from.ParentOffset.y;
                this.ParentOffset.z = from.ParentOffset.z;
                this.Axis.x = from.Axis.x;
                this.Axis.y = from.Axis.y;
                this.Axis.z = from.Axis.z;
                this.Rotation.x = from.Rotation.x;
                this.Rotation.y = from.Rotation.y;
                this.Rotation.z = from.Rotation.z;
                this.Rotation.w = from.Rotation.w;
            }

            //public void ConfigWriteParams(PluginConfiguration config, int servoNumber, FKparams fkParams)
            //{
            //    config.SetValue(servoNumber.ToString() + "-Servo-Params-ParentOffset", fkParams.ParentOffset);
            //    config.SetValue(servoNumber.ToString() + "-Servo-Params-Axis", fkParams.Axis);
            //    config.SetValue(servoNumber.ToString() + "-Servo-Params-Position", fkParams.Position);
            //    config.SetValue(servoNumber.ToString() + "-Servo-Params-Rotation", fkParams.Rotation);

            //    config.save();

            //    Debug.Log(string.Format("[TRF] {0} - ConfigWriteParams( " + servoNumber.ToString() + "-Servo-Params )", 50));

            //    //config.SetValue("controlWindowPosition", _controlWindowPosition);
            //    //config.SetValue("editorWindowPosition", _editorWindowPosition);
            //    //config.SetValue("editorWindowSize", _editorWindowSize);
            //    //config.SetValue("uiSettingsWindowPosition", _settingsWindowPosition);
            //    //config.SetValue("presetsWindowPosition", _presetsWindowPosition);
            //    //config.SetValue("UIAlphaValue", (double)_UIAlphaValue);
            //    //config.SetValue("UIScaleValue", (double)_UIScaleValue);
            //    //config.SetValue("useEC", UseElectricCharge);
            //    //config.SetValue("useBlizzyToolbar", useBlizzyToolbar);

            //    //config.save();
            //}

            //public FKparams ConfigReadParams(PluginConfiguration config, int servoNumber)
            //{
            //    FKparams fkParams = new FKparams();

            //    config.load();

            //    fkParams.ParentOffset = config.GetValue<Vector3>(servoNumber.ToString() + "-Servo-Params-ParentOffset");
            //    fkParams.Axis = config.GetValue<Vector3>(servoNumber.ToString() + "-Servo-Params-Axis");
            //    fkParams.Position = config.GetValue<Vector3>(servoNumber.ToString() + "-Servo-Params-Position");
            //    fkParams.Rotation = config.GetValue<Quaternion>(servoNumber.ToString() + "-Servo-Params-Rotation");

            //    return fkParams;

            //    //config.load();

            //    //_controlWindowPosition = config.GetValue<Vector3>("controlWindowPosition");
            //    //_editorWindowPosition = config.GetValue<Vector3>("editorWindowPosition");
            //    //_editorWindowSize = config.GetValue<Vector2>("editorWindowSize");
            //    //_settingsWindowPosition = config.GetValue<Vector3>("uiSettingsWindowPosition");
            //    //_presetsWindowPosition = config.GetValue<Vector3>("presetsWindowPosition");

            //    //_UIAlphaValue = (float)config.GetValue<double>("UIAlphaValue", 0.8);
            //    //_UIScaleValue = (float)config.GetValue<double>("UIScaleValue", 1.0);
            //    //UseElectricCharge = config.GetValue<bool>("useEC", true);
            //    //useBlizzyToolbar = config.GetValue<bool>("useBlizzyToolbar", false);
            //}
        }

        public class IKservo
        {
            public Part part;
            public IRWrapper.IServo iservo;
            //public Vector3 ParentOffset;
            //public Vector3 Position;
            //public Quaternion Rotation;
            //public Quaternion localRotation;
            //public Vector3 Axis;
            public Transform ServoTransform;
            public string name;
            public float MinAngle = -270f;
            public float MaxAngle = 270f;

            public FKparams fkParams;

            //public Vector3 StartRotOffset;

            public IKservo()
            {
            }

            public IKservo(Part part, IRWrapper.IServo iservo)
            {
                this.part = part;
                this.iservo = iservo;
                this.fkParams = new FKparams();
            }

            public IKservo(Part part)
            {
                this.part = part;
                this.fkParams = new FKparams();
            }

            public IKservo(FKparams fkParams)
            {
                this.fkParams = fkParams;
            }

            public IKservo Clone()
            {
                IKservo destination = new IKservo();

                destination.part = this.part;
                // destination.iservo = this.iservo.Clone();
                destination.iservo = this.iservo;
                destination.ServoTransform = this.ServoTransform;
                destination.name = this.name;
                destination.MinAngle = this.MinAngle;
                destination.MaxAngle = this.MaxAngle;
                destination.fkParams = this.fkParams.Clone();

                return destination;
            }

            public void Copy(IKservo from)
            {
                this.part = from.part;
                this.iservo = from.iservo;
                this.ServoTransform = from.ServoTransform;
                this.name = from.name;
                this.MinAngle = from.MinAngle;
                this.MaxAngle = from.MaxAngle;
                this.fkParams.Copy(from.fkParams);
            }
        }

        Buttons buttons;
        List<IRWrapper.IServo> allServos;
        List<IKservo> AllIkServo;
        List<IKservo> SortIkServo;
        List<IKservo> FKparamsIkServo;
        List<Part> listOfChildrenPart;
        VectorSM FKvector = new VectorSM();
        VectorSM baseState = new VectorSM();
        GameObject[] servoGimbal = new GameObject[8];
        GameObject global = new GameObject();
        Part lastPart;

        GameObject hoverObject;
        Transform dockingNodeObject;

        IKservo orgLEEikServo;
        bool VeeIsActive = false;

        Mode mode;
        int limitDepth = 2;

        private enum Mode
        {
            PART,
            UI,
            OBJECT
        }

        string[] JointList = { "TRF.CA2.ARoll", "TRF.CA2.AYaw", "TRF.CA2.APitch", "TRF.CA2.CElbow", "TRF.CA2.BPitch", "TRF.CA2.BYaw", "TRF.CA2.BRoll", "TRF.CA2.LEE.wCam" };
        Vector3[] JointsAxis = { new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 0) };
        // !!! calculate value of JointsRealAxis !!!
        // PDGF Axis list
        //Vector3[] JointsRealAxis = { new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 0) };
        Vector3[] JointsBaseAxis = { new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, -1, 0), new Vector3(0, 0, 0) };
        // LEE Axis list
        Vector3[] JointsRealAxis = { new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1), new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 0) };
        public float[] theta = new float[8] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
        public float[] currentTheta = new float[8] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

        public bool[] blocking = new bool[8];
        public bool nearPDGFBool = false;
        public float distance;
        public float angle;
        public bool Success = false;
        public bool IsForceDist = false;
        public bool IsForceRoll = false;
        public bool IKsuccess = false;
        public bool Bug = true;

        public Vector3 globalPosition;
        public Quaternion globalQuaternion;

        public int iterateNumber = 500;
        public float forceDistNumberZ = 0.6f;
        public float forceRollNumberZ = 0.0f;
        public float forceDistNumberX = 0.0f;
        public float forceRollNumberX = 0.0f;
        public float forceDistNumberY = 0.0f;
        public float forceRollNumberY = 0.0f;
        public float LearningRatePos;
        public float LearningRateOri;
        public float SamplingAngle = 0.02f;

        public float DistanceThreshold = 0.01f;
        public float Distance;
        public float AngleThreshold = 0.5f;
        public float Angle;

        public float workingDistance;

        public float MaxPosErr = 10f;
        public float MaxOriErr = 180f;

        public float DinLearningRatePos;
        public float DinLearningRateOri;

        public int failedIterateCycle = 0;
        public int iterateThreshold = 10;

        private Vector3 basePosition;
        private Quaternion baseQuaternion;

        private Vector3 partPosition;
        private Quaternion partRotation;

        private Vector3 prevPartPosition;
        private Quaternion prevPartRotation;

        private bool InitIRWrapper()
        {
            //if (!IRWrapper.APIReady)
            //{
                Debug.Log(string.Format("[TRF] {0} IRWrapper.InitWrapper() START", 1));
                IRWrapper.InitWrapper();
                Debug.Log(string.Format("[TRF] {0} IRWrapper.InitWrapper() END", 2));
            //}
            //else
            //    Debug.Log(string.Format("[TRF] {0} InitIRWrapper() IRWrapper.APIReady == true", 1));
            return IRWrapper.APIReady;
        }

        #region mapping servo structure
        private void CreateFKparams()
        {
            FKparamsIkServo = new List<IKservo>();

            for (int i = 0; i < 8; i++)
            {
                FKparamsIkServo.Add(new IKservo(new FKparams()));
            }
        }

        private bool CheckServosZeroState(bool stateToTheta = false)
        {
            if (stateToTheta)
                StatesToThetas();
            foreach (float element in theta)
            {
                if (element != 0f)
                    return false;
            }

            return true;
        }

        private void StatesToThetas()
        {
            // Get angle of Joints
            if (SortIkServo != null)
                for (int j = 0; j < SortIkServo.Count - 1; j++)
                    theta[j] = SortIkServo[j].iservo.Position;
            else
                Debug.Log(string.Format("[TRF] StatesToThetas() SortIkServo == null"));
        }

        private bool CheckExistArm()
        {
            if (part != null)
            {
                for (int i = 0; i < JointList.Length; i++)
                {
                    Debug.Log(string.Format("[TRF] CheckExistArm() - JointList[{0}] = " + JointList[i] + " " + ((part.FindChildPart(JointList[i], true) != null) ? "exist" : "not exist"), i));
                    if (part.FindChildPart(JointList[i], true) == null)
                        return false;
                }

                return true;
            }
            else
                return false;
        }

        private bool GetArmServos()
        {
            Debug.Log(string.Format("[TRF]  GetArmServos() - START"));
            if (IRWrapper.APIReady)
            {
                Debug.Log(string.Format("[TRF] {0} - IRWrapper.APIReady", 21));
                if (AllIkServo == null)
                    AllIkServo = new List<IKservo>();
                if (allServos == null)
                    allServos = new List<IRWrapper.IServo>();
                if (SortIkServo == null)
                {
                    SortIkServo = new List<IKservo>();
                    Debug.Log(string.Format("[TRF] - GetArmServos() - construct SortIkServo"));
                }
                Debug.Log(string.Format("[TRF] {0} - Inited ServoLists", 21));

                GetAllChildServo();
                Debug.Log(string.Format("[TRF] {0} - Get all child servo ({1})", 22, allServos.Count));

                if (allServos.Count >= JointList.Length)
                {
                    Debug.Log(string.Format("[TRF] {0} - AllIkServo ({1})", 23, AllIkServo.Count));

                    SortIKservos();

                    // Get last part
                    if (listOfChildrenPart == null)
                        listOfChildrenPart = new List<Part>();
                    GetChildPartRecursive(part);
                    int elementCount = listOfChildrenPart.Count();
                    lastPart = listOfChildrenPart.Find(e => e.name == JointList[JointList.Length - 1]);

                    Debug.Log(string.Format("[TRF] {0} - lastPart " + lastPart.name, 40));

                    // Add EndEffector/LEE/ (last part) to SortIkServo - problem -> if LEE hold other element that will last part
                    SortIkServo.Add(new IKservo(lastPart));
                    Debug.Log(string.Format("[TRF] {0} - END - SortIkServo.Add(new IKservo(lastPart)", 41));
                }
                else
                {
                    Debug.Log(string.Format("[TRF] - GetArmServos() - allServos.Count < JointList.Length"));
                    return false;
                }
            }
            else
                return false;
            Debug.Log(string.Format("[TRF]  GetArmServos() - END"));
            return true;
        }

        private void GetAllChildServo()
        {
            // Get all servo
            foreach (IRWrapper.IControlGroup group in IRWrapper.IRController.ServoGroups)
            {
                allServos.AddRange(group.Servos);
            }

            foreach (IRWrapper.IServo element in allServos)
            {
                Debug.Log(string.Format("[TRF] {0} - allServos.Add( " + element.HostPart.name + " )", 22));
            }

            // Scanning all servos
            foreach (IRWrapper.IServo iservo in allServos)
            {
                //Debug.Log(string.Format("[TRF] {0} - iservo.HostPart.name( " + iservo.HostPart.name + " )", 22));
                foreach (Part element in part.FindChildParts<Part>(true))
                {
                    //Debug.Log(string.Format("[TRF] {0} - FindChildParts<Part>( " + element.name + " )", 22));
                    // if childparts contain servo and not hinge then add to IKservos
                    if ((iservo.HostPart.name == element.name) && !iservo.HostPart.name.Contains("Hinge"))
                    {
                        AllIkServo.Add(new IKservo(element, iservo));
                        Debug.Log(string.Format("[TRF] {0} - AllIkServo.Add( " + element.name + " )", 22));
                    }
                }
            }

            Debug.Log(string.Format("[TRF] {0} - END GetAllChildServo()", 22));
        }

        private void RemoveHingeServo()
        {
            foreach (IRWrapper.IServo iservo in allServos)
            {
                if (!iservo.HostPart.name.Contains("Hinge"))
                {
                    AllIkServo.Add(new IKservo(part, iservo));
                }
            }

            Debug.Log(string.Format("[TRF] {0} - END RemoveHingeServo()", 23));
        }

        //private void RemoveFalseServo()
        //{
        //    foreach (IRWrapper.IServo iservo in allServos)
        //    {
        //        if (Array.IndexOf(JointList, iservo.HostPart.name) > -1)
        //        {
        //            AllIkServo.Add(new IKservo(part, iservo));
        //            Debug.Log(string.Format("[TRF] {0} - AllIkServo.Add( " + part.name + " )", 23));

        //        }
        //    }

        //    Debug.Log(string.Format("[TRF] {0} - END RemoveFalseServo()", 23));
        //}

        private void SortIKservos()
        {
            for (int i = 0; i < JointList.Length; i++)
            {
                foreach (IKservo ikServo in AllIkServo)
                {
                    if (ikServo.part.name == JointList[i])
                    {
                        SortIkServo.Add(ikServo);
                        Debug.Log(string.Format("[TRF] {0} - ikServo " + ikServo.part.name, 24));
                    }
                }
            }

            Debug.Log(string.Format("[TRF] {0} - END SortIKservos()", 24));
        }

        private bool DumpServoStructure(List<IKservo> IkServos)
        {
            // 
            Vector3 StartPos = part.transform.position;
            // 
            Quaternion StartRot = part.transform.rotation;
            int i = 0;

            // dumping servo structure
            foreach (IKservo ikServo in IkServos)
            {
                if (i < (IkServos.Count - 1))
                {
                    // transform of servo
                    ikServo.ServoTransform = ikServo.part.transform;
                    // orientation of main part
                    ikServo.fkParams.PartRotation = part.transform.rotation;
                    // position offset for parentpart
                    ikServo.fkParams.ParentOffset = Quaternion.Inverse(StartRot) * (ikServo.ServoTransform.position - StartPos);
                    // axis of servo
                    ikServo.fkParams.Axis = part.transform.rotation * JointsRealAxis[i];
                    // global rotation of servo
                    ikServo.fkParams.Rotation = ikServo.ServoTransform.rotation;
                    // Parent's global position of servo
                    StartPos = ikServo.ServoTransform.position;
                    // Parent's global rotation of servo
                    StartRot = ikServo.fkParams.Rotation;
                }
                else
                {
                    foreach (Transform element in ikServo.part.GetComponentsInChildren<Transform>())
                    {
                        if (element.name == "dockingNode")
                        {
                            // transform of servo
                            ikServo.ServoTransform = element.transform;
                            // orientation of main part
                            ikServo.fkParams.PartRotation = part.transform.rotation;
                            // position offset for parentpart
                            ikServo.fkParams.ParentOffset = Quaternion.Inverse(StartRot) * (element.transform.position - StartPos);
                            // axis of servo
                            ikServo.fkParams.Axis = part.transform.rotation * JointsRealAxis[i];
                            // global rotation of servo
                            ikServo.fkParams.Rotation = element.transform.rotation;
                            // Parent's global position of servo
                            StartPos = element.transform.position;
                            // Parent's global rotation of servo
                            StartRot = ikServo.fkParams.Rotation;

                            break;
                        }
                    }
                }

                Debug.Log(string.Format("[TRF] {0} - ikServo " + ikServo.part.name, 40));
                Debug.Log(string.Format("[TRF] {0} - ParentOffset " + VectorToString(ikServo.fkParams.ParentOffset, "0.00"), 42));
                Debug.Log(string.Format("[TRF] {0} - Axis " + VectorToString(ikServo.fkParams.Axis, "0.0"), 40));
                Debug.Log(string.Format("[TRF] {0} - Rotation " + QuaternionToString(ikServo.fkParams.Rotation, "0.00"), 42));

                i++;
            }

            Debug.Log(string.Format("[TRF] {0} - END DumpServoStructure()", 42));

            Debug.Log(string.Format("[TRF] {0} - part.transform.rotation " + QuaternionToString(part.transform.rotation, "0.00"), 42));

            return true;
        }
        #endregion mapping servo structure

         private bool initPartConfigButtonColors()
        {
            return (ColorUtility.TryParseHtmlString(controlButtonUpDownColor, out colorControlButtonUpDown)
                && ColorUtility.TryParseHtmlString(controlButtonLeftRightColor, out colorControlButtonLeftRight)
                && ColorUtility.TryParseHtmlString(controlButtonForwardBackwardColor, out colorControlButtonForwardBackward));
        }

        // Use this for initialization
        public void Start()
        {
            buttonIsColored = initPartConfigButtonColors();
            IsInitedModule = false;
            msgWindowRectangle = new Rect(msgWindowPosition.x, msgWindowPosition.y, msgWindowSize.x, msgWindowSize.y);

            if (HighLogic.LoadedSceneIsFlight)
            {
                if (FKparamsIkServo == null)
                    CreateFKparams();

                WindowID = NextWindowID;
                NextWindowID++;
                MsgWindowID = NextWindowID;
                NextWindowID++;

                if (part == null)
                    Debug.Log(string.Format("[TRF{1}] {0} Start() part == null", 0, WindowID));
                else
                    Debug.Log(string.Format("[TRF{1}] {0} Start() part.name = " + part.name, 0, WindowID));

                diagramRectangle = new Rect(10, 525, 290, 70);

                //// Set base angle of joints = 0 degree
                //theta[0] = theta[1] = theta[2] = theta[3] = theta[4] = theta[5] = theta[6] = theta[7] = 0.0f;

                blocking[0] = blocking[1] = blocking[2] = blocking[3] = blocking[4] = blocking[5] = blocking[6] = blocking[7] = false;
                // Init Buttons
                buttons = new Buttons();

                // Set start position and  start orientation of EndEffector
                globalPosition = new Vector3(0f, 0f, 0f);
                globalQuaternion = Quaternion.Euler(0f, 0f, 0f);

                Debug.Log(string.Format("[TRF] {0} - START - EndDeclaration", 48));
                windowRectangle = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
                iterateData = new List<float>();
                sampleAngleData = new List<float>();
                data = new List<List<float>>();
                Debug.Log(string.Format("[TRF] {0} - END - EndDeclaration", 49));

                Debug.Log(string.Format("[TRF] {0} end of Start() WindowID: {1}", 400, WindowID));

                //// check exist arm on part
                //if (!CheckExistArm())
                //{
                //    Debug.Log(string.Format("[TRF] {0} Don't Exist Arm on PART", 0));
                //    return;
                //}
                //else
                //{
                //    Debug.Log(string.Format("[TRF] {0} Exist Arm on PART", 0));
                //}

                //Debug.Log(string.Format("[TRF] {0} IRWrapper.InitWrapper() START", 1));
                //IRWrapper.InitWrapper();
                //Debug.Log(string.Format("[TRF] {0} IRWrapper.InitWrapper() END", 2));

                //if (IRWrapper.APIReady)
                //{
                //    Debug.Log(string.Format("[TRF] {0} - IRWrapper.APIReady", 21));
                //    AllIkServo = new List<IKservo>();
                //    allServos = new List<IRWrapper.IServo>();
                //    SortIkServo = new List<IKservo>();
                //    Debug.Log(string.Format("[TRF] {0} - Inited ServoLists", 21));

                //    GetAllChildServo();
                //    Debug.Log(string.Format("[TRF] {0} - Get all child servo ({1})", 22, allServos.Count));

                //    if (allServos.Count >= JointList.Length)
                //    {
                //        Debug.Log(string.Format("[TRF] {0} - AllIkServo ({1})", 23, AllIkServo.Count));

                //        SortIKservos();

                //        // Get last part
                //        listOfChildrenPart = new List<Part>();
                //        GetChildPartRecursive(part);
                //        int elementCount = listOfChildrenPart.Count();
                //        //lastPart = listOfChildrenPart[elementCount - 1];
                //        lastPart = listOfChildrenPart.Find(e => e.name == JointList[JointList.Length - 1]);

                //        Debug.Log(string.Format("[TRF] {0} - lastPart " + lastPart.name, 40));

                //        // Add EndEffector/LEE/ (last part) to SortIkServo - problem -> if LEE hold other element that will last part
                //        SortIkServo.Add(new IKservo(lastPart));
                //        Debug.Log(string.Format("[TRF] {0} - END - SortIkServo.Add(new IKservo(lastPart)", 41));

                //        // if have all IKservo than calculate IK parameters of IKservos 
                //        if (SortIkServo.Count == JointList.Length)
                //        {
                //            partPosition = part.transform.position;
                //            partRotation = part.transform.rotation;

                //            // dumping servo structure
                //            DumpServoStructure(SortIkServo);

                //            //Debug.Log(string.Format("[TRF] {0} - START - GetFKParamsFromBuffer()", 42));
                //            GetFKParamsFromBuffer();
                //            //Debug.Log(string.Format("[TRF] {0} - END - GetFKParamsFromBuffer()", 43));

                //        }
                //    }
                //}
                //else
                //    return;

                if (!InitIRWrapper())
                    return;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - InitIRWrapper() == true"));
                if (!CheckExistArm())
                    return;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - CheckExistArm() == true"));
                if (!GetArmServos())
                    return;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - GetArmServos() == true"));
                ServosIsBaseInited = true;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - ServosIsBaseInited = true"));
                if (!DumpServoStructure(SortIkServo))
                    return;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - DumpServoStructure(SortIkServo) == true"));
                if (!IsLoadedFkParams)
                {
                    Debug.Log(string.Format("[TRF] >>> >>> Start() - IsLoadedFkParams == false"));
                    //if (!CheckServosZeroState(true))
                    //    return;
                    //Debug.Log(string.Format("[TRF] >>> >>> Start() - CheckServosZeroState() == true"));
                    IsLoadedFkParams = true;
                    SetFKParamsToBuffer();
                    Debug.Log(string.Format("[TRF] >>> >>> Start() - SetFKParamsToBuffer() == true"));
                    OnSave(null);
                    Debug.Log(string.Format("[TRF] >>> >>> Start() - OnSave()"));
                }
                if (!GetFKParamsFromBuffer())
                    return;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - GetFKParamsFromBuffer() == true"));
                IsInitedModule = true;
                Debug.Log(string.Format("[TRF] >>> >>> Start() - IsInitedModule = true"));
            }
        }

        // Update is called once per frame
        public void Update()
        {
            //if (HighLogic.LoadedSceneIsFlight && ServosIsBaseInited && !DumpEventIsActive)
            //{
            //    //Debug.Log(string.Format("[TRF] Update() - DumpEventIsActive = false"));
            //    if (CheckServosZeroState())
            //    {
            //        CheckServosZeroState(true);
            //        Events["DumpServoStructureEvent"].active = true;
            //        DumpEventIsActive = true;
            //        Debug.Log(string.Format("[TRF] Update() - DumpEventIsActive = true"));
            //    }
            //}

            if (HighLogic.LoadedSceneIsFlight && IsInitedModule && IsInitedModule1st)
            {
                IsInitedModule1st = false;
                Debug.Log(string.Format("[TRF] Update() - IsInitedModule1st = false"));

                Debug.Log(string.Format("[TRF] {0} - Update() - globalPosition&Quaternion", 44));
                // set position of last part for global position
                //if (globalPosition == null)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - globalPosition == null"));
                //    return;
                //}
                //if (globalQuaternion == null)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - globalQuaternion == null"));
                //    return;
                //}
                //if (SortIkServo == null)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - SortIkServo == null"));
                //    return;
                //}
                //if (SortIkServo.Count == 0)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - SortIkServo.Count == 0"));
                //    return;
                //}
                //Debug.Log(string.Format("[TRF] - Update() - SortIkServo.Count = {0}", SortIkServo.Count));
                //int counter = 0;
                //foreach (IKservo element in SortIkServo)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - counter = {0}", counter));
                //    counter++;
                //    if (element == null)
                //    {
                //        Debug.Log(string.Format("[TRF] - Update() - element == null"));
                //        return;
                //    }
                //}
                //Debug.Log(string.Format("[TRF] - Update() - SortIkServo.Count - 1 = {0}", (SortIkServo.Count - 1)));
                //counter = SortIkServo.Count - 1;
                //if (SortIkServo[counter].ServoTransform.position != null)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - SortIkServo[counter].ServoTransform.position = " + VectorToString(SortIkServo[counter].ServoTransform.position, "0.00")));
                globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                //}
                //else
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - SortIkServo[counter].ServoTransform.position == null"));
                //    return;
                //}
                //if (SortIkServo[counter].ServoTransform.rotation != null)
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - SortIkServo[counter].ServoTransform.rotation = " + QuaternionToString(SortIkServo[counter].ServoTransform.rotation, "0.00")));
                globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;
                //}
                //else
                //{
                //    Debug.Log(string.Format("[TRF] - Update() - SortIkServo[counter].ServoTransform.rotation == null"));
                //    return;
                //}
                Debug.Log(string.Format("[TRF] {0} - Update() - globalPosition&Quaternion", 45));

                baseState.Translation = globalPosition;
                baseState.Rotation = globalQuaternion;
                Debug.Log(string.Format("[TRF] Update() -  baseState init"));

                // Store base position and base orientation
                basePosition = globalPosition;
                baseQuaternion = globalQuaternion;
                Debug.Log(string.Format("[TRF] Update() -  (basePosition, baseQuaternion) init"));

                for (int i = 0; i < SortIkServo.Count; i++)
                    servoGimbal[i] = new GameObject();
                Debug.Log(string.Format("[TRF] Update() -  baseState init"));

                // Get angle of Joints
                for (int j = 0; j < SortIkServo.Count - 1; j++)
                    theta[j] = SortIkServo[j].iservo.Position;
                Debug.Log(string.Format("[TRF] Update() -  Get angle of Joints"));

                prevPartPosition = part.transform.position;
                prevPartRotation = part.transform.rotation;
                Debug.Log(string.Format("[TRF] Update() -  (prevPartPosition, prevPartRotation) init"));

                // cPos - tartget position = current arm position
                globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;
                Debug.Log(string.Format("[TRF] Update() -  (globalPosition, globalQuaternion) init"));

                Events["TurnOnIKRCEvent"].active = true;
            }

            //// Dump Button enable / disable
            //if (HighLogic.LoadedSceneIsFlight && (theta[0] == 0 && theta[1] == 0 && theta[2] == 0 && theta[3] == 0 && theta[4] == 0 && theta[5] == 0 && theta[6] == 0 && theta[7] == 0))
            //{
            //    Events["DumpServoStructureEvent"].active = true;
            //}
            //else
            //{
            //    Events["DumpServoStructureEvent"].active = false;
            //}

            if (ircWindowActive && HighLogic.LoadedSceneIsFlight && IsInitedModule)
            {
                // Set Aim Object (PDGFx)
                GameObject mouseObject = CheckForObjectUnderCursor();
                bool modPressed = Input.GetKey(KeyCode.LeftAlt);
                if (modPressed && (mouseObject != null))
                {
                    foreach (Transform element in mouseObject.GetComponentsInChildren<Transform>())
                    {
                        if (element.name == "dockingNode")
                        {
                            dockingNodeObject = element;
                            hoverObject = mouseObject;
                            Debug.Log(string.Format("[TRF] {0} - hoverObject " + hoverObject.name, 501));
                            Debug.Log(string.Format("[TRF] {0} - dockingNodeObject = " + dockingNodeObject.name, 502));
                            break;
                        }
                    }
                }
                if (modPressed && (mouseObject == null))
                {
                    dockingNodeObject = null;
                    hoverObject = null;
                }

                // emergency servo stop
                if (emergencyServoStop)
                {
                    IK_active = false;
                    servoSpeed = 0f;
                    globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                    globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;
                }

                // set base state
                if (baseStateBool)
                {
                    globalPosition = basePosition;
                    globalQuaternion = baseQuaternion;

                    for (int i = 0; i < 8; i++)
                        theta[i] = 0f;

                    IK_active = false;

                    // implement angle of joints
                    Bug = !ImplementServoRotation(SortIkServo, theta, servoSpeed);
                }

                // set actual state
                if (actualStateBool)
                {
                    FKvector = ForwardKinematics(theta, SortIkServo);
                    globalPosition = FKvector.Translation;
                    globalQuaternion = FKvector.Rotation;

                    IK_active = false;
                }

                #region PdgfAimObject
                // set pdgf state - set aim object -> deactivate IK function
                if (pdgfStateBool && (dockingNodeObject != null))
                {
                    if (!nearPDGFBool)
                    {
                        //globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, -90f) * (IsForceRoll ? Quaternion.Euler(0f, 0f, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, 180f) * (IsForceRoll ? Quaternion.Euler(forceRollNumberX, forceRollNumberY, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        //globalPosition = dockingNodeObject.position + (globalQuaternion * (IsForceDist ? new Vector3(0f, 0f, -1.0f * forceDistNumberZ) : new Vector3(0f, 0f, -0.6f)));
                        globalPosition = dockingNodeObject.position + (globalQuaternion * (IsForceDist ? new Vector3(forceDistNumberX, forceDistNumberY, -1.0f * forceDistNumberZ) : new Vector3(0f, 0f, -0.6f)));
                        if (TargetInRange(ArmMaxLength))
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - !nearPDGFBool - TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                        }
                        else
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - !nearPDGFBool - !TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                            globalPosition = TargetToRange(ArmMaxLength);
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = TargetToRange(ArmMaxLength) = " + VectorToString(globalPosition, "0.00")));
                        }
                    }
                    else
                    {
                        //globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, -90f) * (IsForceRoll ? Quaternion.Euler(0f, 0f, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, 180f) * (IsForceRoll ? Quaternion.Euler(forceRollNumberX, forceRollNumberY, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        globalPosition = dockingNodeObject.position + (globalQuaternion * new Vector3(0.0f, 0.0f, 0.0f));
                        if (TargetInRange(ArmMaxLength))
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - nearPDGFBool - TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                        }
                        else
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - nearPDGFBool - !TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                            globalPosition = TargetToRange(ArmMaxLength);
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = TargetToRange(ArmMaxLength) = " + VectorToString(globalPosition, "0.00")));
                        }
                    }
                    IK_active = false;

                    //for (int i = 0; i < servoGimbal.Length; i++)
                    //{
                    //    Debug.Log(string.Format("[TRF] {0} - Update() - (servoGimbal[{1}]) " + VectorToString(servoGimbal[i].transform.position, "0.00") + " " + VectorToString(servoGimbal[i].transform.rotation.eulerAngles, "0.00"), 599, i));
                    //}
                }

                if (refreshPDGFBool && (dockingNodeObject != null))
                {
                    //if (!nearPDGFBool)
                    //{
                    //    globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, -90f) * (IsForceRoll ? Quaternion.Euler(0f, 0f, forceRollNumber) : Quaternion.Euler(0f, 0f, 0f)));
                    //    globalPosition = dockingNodeObject.position + (globalQuaternion * (IsForceDist ? new Vector3(0f, 0f, -1.0f * forceDistNumber) : new Vector3(0f, 0f, -0.6f)));
                    //}
                    //else
                    //{
                    //    globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, -90f) * (IsForceRoll ? Quaternion.Euler(0f, 0f, forceRollNumber) : Quaternion.Euler(0f, 0f, 0f)));
                    //    globalPosition = dockingNodeObject.position + (globalQuaternion * new Vector3(0.0f, 0.0f, 0.0f));
                    //}

                    if (!nearPDGFBool)
                    {
                        //globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, -90f) * (IsForceRoll ? Quaternion.Euler(0f, 0f, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, 180f) * (IsForceRoll ? Quaternion.Euler(forceRollNumberX, forceRollNumberY, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        //globalPosition = dockingNodeObject.position + (globalQuaternion * (IsForceDist ? new Vector3(0f, 0f, -1.0f * forceDistNumberZ) : new Vector3(0f, 0f, -0.6f)));
                        globalPosition = dockingNodeObject.position + (globalQuaternion * (IsForceDist ? new Vector3(forceDistNumberX, forceDistNumberY, -1.0f * forceDistNumberZ) : new Vector3(0f, 0f, -0.6f)));
                        if (TargetInRange(ArmMaxLength))
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - !nearPDGFBool - TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                        }
                        else
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - !nearPDGFBool - !TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                            globalPosition = TargetToRange(ArmMaxLength);
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = TargetToRange(ArmMaxLength) = " + VectorToString(globalPosition, "0.00")));
                        }
                    }
                    else
                    {
                        //globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, -90f) * (IsForceRoll ? Quaternion.Euler(0f, 0f, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        globalQuaternion = dockingNodeObject.rotation * (Quaternion.Euler(180f, 0f, 180f) * (IsForceRoll ? Quaternion.Euler(forceRollNumberX, forceRollNumberY, forceRollNumberZ) : Quaternion.Euler(0f, 0f, 0f)));
                        globalPosition = dockingNodeObject.position + (globalQuaternion * new Vector3(0.0f, 0.0f, 0.0f));
                        if (TargetInRange(ArmMaxLength))
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - nearPDGFBool - TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                        }
                        else
                        {
                            Debug.Log(string.Format("[TRF] - Update() - PdgfAimObject - nearPDGFBool - !TargetInRange"));
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = " + VectorToString(globalPosition, "0.00")));
                            globalPosition = TargetToRange(ArmMaxLength);
                            Debug.Log(string.Format("[TRF] - Update() - globalPosition = TargetToRange(ArmMaxLength) = " + VectorToString(globalPosition, "0.00")));
                        }
                    }

                    for (int i = 0; i < servoGimbal.Length; i++)
                    {
                        Debug.Log(string.Format("[TRF] {0} - Update() - (servoGimbal[{1}]) " + VectorToString(servoGimbal[i].transform.position, "0.00") + " " + VectorToString(servoGimbal[i].transform.rotation.eulerAngles, "0.00"), 599, i));
                    }
                }
                #endregion PdgfAimObject

                #region VirtualEndEffector
                // set selected part than virtual endeffector
                if (veeStateBool && (dockingNodeObject != null) && !VeeIsActive)
                {
                    Debug.Log(string.Format("[TRF] {0} - Update() - set selected part than virtual endeffector", 600));
                    IK_active = false;
                    VeeIsActive = true;

                    IKservo eefIKservo = SortIkServo[SortIkServo.Count - 1];
                    IKservo prevIKservo = SortIkServo[SortIkServo.Count - 2];

                    // save original LEE IKservo parameters
                    orgLEEikServo = eefIKservo.Clone();
                    Debug.Log(string.Format("[TRF] {0} - Update() - save original LEE IKservo parameters", 600));
                    //Debug.Log(string.Format("[TRF] {0} - Update() - (SortIkServo[SortIkServo.Count - 1]) " + IKServoToString(SortIkServo[SortIkServo.Count - 1]), 600));
                    Debug.Log(string.Format("[TRF] {0} - Update() - (eefIKservo) " + IKServoToString(eefIKservo), 600));
                    Debug.Log(string.Format("[TRF] {0} - Update() - (orgLEEikServo) " + IKServoToString(orgLEEikServo), 600));

                    // change FKParams of LEE (last servo object)

                    Part partOfHoverObject = hoverObject.GetComponentInParent<Part>() as Part;
                    eefIKservo.part = partOfHoverObject;
                    eefIKservo.name = partOfHoverObject.name;
                    Debug.Log(string.Format("[TRF] {0} - Update() - partOfHoverObject.name = " + partOfHoverObject.name, 600));
                    //eefIKservo.iservo = 
                    eefIKservo.MinAngle = 0f;
                    eefIKservo.MaxAngle = 0f;

                    Debug.Log(string.Format("[TRF] {0} - Update() - searching for dockingNode Transform", 600));
                    foreach (Transform element in partOfHoverObject.GetComponentsInChildren<Transform>())
                    {
                        Debug.Log(string.Format("[TRF] {0} - Update() - element.name =" + element.name, 600));
                        if (element.name == "dockingNode")
                        {
                            // transform of servo
                            eefIKservo.ServoTransform = element.transform;
                            // orientation of main part
                            eefIKservo.fkParams.PartRotation = part.transform.rotation;
                            // position offset for parentpart
                            eefIKservo.fkParams.ParentOffset = Quaternion.Inverse(prevIKservo.part.transform.rotation) * (element.transform.position - prevIKservo.part.transform.position);
                            // axis of servo
                            eefIKservo.fkParams.Axis = part.transform.rotation * new Vector3(0, 0, 0);
                            // global rotation of servo
                            Quaternion basePartRotationDiff = part.transform.rotation * Quaternion.Inverse(partRotation);
                            for (int i = 0; i < (SortIkServo.Count - 1); i++)
                                basePartRotationDiff *= Quaternion.AngleAxis(theta[i], SortIkServo[i].fkParams.Axis);
                            eefIKservo.fkParams.Rotation = Quaternion.Inverse(basePartRotationDiff) * element.transform.rotation;
                            Debug.Log(string.Format("[TRF] {0} - Update() - set new FKParams values", 600));
                            break;
                        }
                    }

                    // initialize actual position and rotation
                    Debug.Log(string.Format("[TRF] {0} - Update() - initialize actual position and rotation", 600));
                    globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                    globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;
                }

                if (veeStateBool && (dockingNodeObject == null) && VeeIsActive)
                {
                    Debug.Log(string.Format("[TRF] {0} - Update() - reset original LEE than virtual endeffector", 600));
                    // restore original LEE IKservo parameters
                    Debug.Log(string.Format("[TRF] {0} - Update() - (SortIkServo[SortIkServo.Count - 1]) " + IKServoToString(SortIkServo[SortIkServo.Count - 1]), 601));
                    SortIkServo[SortIkServo.Count - 1].Copy(orgLEEikServo);
                    Debug.Log(string.Format("[TRF] {0} - Update() - (SortIkServo[SortIkServo.Count - 1]) " + IKServoToString(SortIkServo[SortIkServo.Count - 1]), 601));
                    IK_active = false;
                    VeeIsActive = false;
                    Debug.Log(string.Format("[TRF] {0} - Update() - VeeIsActive = false", 601));

                    // initialize actual position and rotation
                    globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                    globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;
                }
                #endregion VirtualEndEffector

                #region RobotArm MovingButtons
                // detect button activity
                IKButton_active = !ButtonsIsReleased();

                // Kiterjesztett endeffektor esetén ez a funkció tiltott kell, hogy legyen !!! ???
                // if rotate only servo of LEE then not IK only rotate servo to direct
                if (buttons.Rotation == Quaternion.Euler(0f, 0f, 0.5f * rotStep) || buttons.Rotation == Quaternion.Euler(0f, 0f, -0.5f * rotStep))
                {
                    theta[6] += (360f - buttons.Rotation.eulerAngles.z) > 180f ? buttons.Rotation.eulerAngles.z : (buttons.Rotation.eulerAngles.z - 360f);
                    // implement angle of joints
                    Bug = !ImplementServoRotation(SortIkServo, theta, servoSpeed);
                }
                #endregion RobotArm MovingButtons

                // calculate global position of aim from buttons command
                // global orientation
                globalQuaternion = globalQuaternion * buttons.Rotation;
                // global position
                globalPosition = globalPosition + globalQuaternion * buttons.Translation;

                // clear buttons.Translation & .Rotation
                ButtonsReleased();

                #region Active Inverse Kinematics
                if (IK_active || IKButton_active)
                {
                    // calculate position- and orientation different
                    distance = Vector3.Distance(SortIkServo[SortIkServo.Count - 1].ServoTransform.position, globalPosition);
                    //angle = Quaternion.Angle(Quaternion.Euler(SortIkServo[SortIkServo.Count - 2].ServoTransform.rotation.eulerAngles), globalQuaternion);
                    angle = Quaternion.Angle(Quaternion.Euler(SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation.eulerAngles), globalQuaternion);

                    // if differents over the threshold then calculate IK
                    if (distance > DistanceThreshold || angle > AngleThreshold)
                    {
                        Success = false;

                        // start iterate from zero thetas
                        if (zeroThetaIterate && IKsuccess)
                        {
                            for (int i = 0; i < 8; i++)
                                theta[i] = 0f;
                        }
                        // clear thetas
                        if (clearThetasBool)
                            for (int i = 0; i < 8; i++)
                                theta[i] = 0f;

                        //if(IKButton_active)
                        //{
                        //    SamplingAngle = 0.01f * transStep;
                        //}

                        // iterate angle of joints
                        int j;
                        for (j = 0; j < iterateNumber; j++)
                        {
                            IKsuccess = InverseKinematics(globalPosition, globalQuaternion, theta);
                            if (IKsuccess)
                                break;
                        }

                        iterateData.Add((float)j / (float)iterateNumber * (diagramRectangle.height - 20f));
                        if (iterateData.Count > (int)(diagramRectangle.width - 10f))
                            iterateData.RemoveAt(0);

                        if (!nearPDGFBool)
                        {
                            if (j == iterateNumber)
                            {
                                failedIterateCycle++;
                                SamplingAngle = 0.005f;
                            }
                            else
                            {
                                failedIterateCycle = 0;
                                if (j > (iterateNumber / 2))
                                {
                                    SamplingAngle = 0.01f;
                                }
                                else
                                {
                                    SamplingAngle = 0.02f;
                                }
                            }
                        }
                        else
                        {
                            SamplingAngle = 0.005f;
                            servoSpeed = 0.031f;
                        }



                        samplingAngleString = SamplingAngle.ToString();

                        sampleAngleData.Add(SamplingAngle / 0.02f * (diagramRectangle.height - 30f));
                        if (sampleAngleData.Count > (int)(diagramRectangle.width - 10f))
                            sampleAngleData.RemoveAt(0);

                        if (failedIterateCycle > iterateThreshold)
                        {
                            failedIterateCycle = 0;

                            for (int i = 0; i < 8; i++)
                                theta[i] = 0f;
                        }

                        // implement angle of joints
                        Bug = !ImplementServoRotation(SortIkServo, theta, servoSpeed);
                    }
                    else
                    {
                        Success = true;
                        // if IK success then turn off IK activity or switchable ???
                        if (!refreshPDGFBool)
                            IK_active = false;
                    }
                    //Debug.Log(string.Format("[TRF] {0} - Update() END ", 107));
                }
                #endregion Active Inverse Kinematics
                #region Inactive Inverse Kinematics
                else
                {
                    // clear buttons.Translation & .Rotation
                    ButtonsReleased();
                    //Debug.Log(string.Format("[TRF] {0} - ButtonsReleased()", 100));
                    FKvector = ForwardKinematics(theta, SortIkServo);
                    //Debug.Log(string.Format("[TRF] {0} - ForwardKinematics(..)", 101));

                    // implement angle of joints
                    Bug = !ImplementServoRotation(SortIkServo, theta, servoSpeed);

                    for (int i = 0; i < 7; i++)
                    {
                        if (blocking[i])
                        {
                            SortIkServo[i].MaxAngle = 0f;
                            SortIkServo[i].MinAngle = 0f;
                        }
                        else
                        {
                            SortIkServo[i].MaxAngle = 270f;
                            SortIkServo[i].MinAngle = -270f;
                        }
                    }
                }
                #endregion Inactive Inverse Kinematics

                #region Refresh LineDiagram Data
                // Refresh data List of LineDiagram
                if (data != null)
                {
                    data.Clear();
                    data.Add(iterateData);
                    data.Add(sampleAngleData);
                }
                #endregion Refresh LineDiagram Data

                global.transform.position = globalPosition;
                global.transform.rotation = globalQuaternion;

                currentTheta = GetActualServoRotation(SortIkServo);
                workingDistance = ArmWorkingDistance();
            }
        }

        #region get part transform by mouse
        private GameObject CheckForObjectUnderCursor()
        {
            //if (EventSystem.current.IsPointerOverGameObject())
            //{
            //    return null;
            //}


            //1000000000000000000101

            if (mode == Mode.UI)
            {
                var pointer = new PointerEventData(EventSystem.current);
                pointer.position = Input.mousePosition;

                var raycastResults = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycastResults);

                if (raycastResults.Count == 0)
                {
                    //print("Nothing");
                    return null;
                }
                return raycastResults[0].gameObject;
            }

            if (mode == Mode.PART)
            {
                //if (Mouse.HoveredPart ? Mouse.HoveredPart.gameObject : null != null)
                //{
                //    Debug.Log(string.Format("[TRF] {0} Mode.PART " + Mouse.HoveredPart.gameObject.ToString(), 410));
                //}
                //else
                //{
                //    Debug.Log(string.Format("[TRF] {0} Mode.PART null", 410));
                //}

                return Mouse.HoveredPart ? Mouse.HoveredPart.gameObject : null;
            }

            if (mode == Mode.OBJECT)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                //int layerMask = ~LayerMask.NameToLayer("UI");
                int layerMask = ~0;

                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, Mathf.Infinity, layerMask))
                {
                    return null;
                }
                return hit.collider.gameObject;
            }

            return null;
        }

        private GameObject GetRootObject(GameObject leaf)
        {
            if (leaf == null)
                return null;

            if (mode == Mode.UI)
            {
                int d = 0;
                while (leaf.transform.parent && !leaf.transform.parent.gameObject.GetComponent<Canvas>() && d < limitDepth)
                {
                    leaf = leaf.transform.parent.gameObject;
                    d++;
                }
                return leaf;
            }

            if (mode == Mode.PART)
            {
                return leaf;
            }

            if (mode == Mode.OBJECT)
            {
                int d = 0;
                while (leaf.transform.parent && d < limitDepth)
                {
                    leaf = leaf.transform.parent.gameObject;
                    d++;
                }
                return leaf;
            }

            return null;
        }
        #endregion get part transform by mouse

        #region KSPEvents
        [KSPEvent(guiActive = true, guiName = "Dump ServoStructure", active = true)]
        public void DumpServoStructureEvent()
        {
            // Genarate servo structure and save
            if (FKparamsIkServo == null)
                CreateFKparams();

            if (part == null)
                Debug.Log(string.Format("[TRF{1}] {0} START Update() part == null", 0, WindowID));
            else
                Debug.Log(string.Format("[TRF{1}] {0} START Update() part.name = " + part.name, 0, WindowID));

            // check exist arm on part
            if (!CheckExistArm())
            {
                Debug.Log(string.Format("[TRF] {0} Don't Exist Arm on PART", 0));
                return;
            }
            else
            {
                Debug.Log(string.Format("[TRF] {0} Exist Arm on PART", 0));
            }

            //Debug.Log(string.Format("[TRF] {0} IRWrapper.InitWrapper() START", 1));
            //IRWrapper.InitWrapper();
            //Debug.Log(string.Format("[TRF] {0} IRWrapper.InitWrapper() END", 2));

            if (IRWrapper.APIReady)
            {
                Debug.Log(string.Format("[TRF] {0} - IRWrapper.APIReady", 21));
                AllIkServo = new List<IKservo>();
                allServos = new List<IRWrapper.IServo>();
                SortIkServo = new List<IKservo>();
                Debug.Log(string.Format("[TRF] {0} - Inited ServoLists", 21));

                GetAllChildServo();
                Debug.Log(string.Format("[TRF] {0} - Get all child servo ({1})", 22, allServos.Count));

                if (allServos.Count >= JointList.Length)
                {
                    Debug.Log(string.Format("[TRF] {0} - AllIkServo ({1})", 23, AllIkServo.Count));

                    SortIKservos();

                    // Get last part
                    listOfChildrenPart = new List<Part>();
                    GetChildPartRecursive(part);
                    int elementCount = listOfChildrenPart.Count();
                    //lastPart = listOfChildrenPart[elementCount - 1];
                    lastPart = listOfChildrenPart.Find(e => e.name == JointList[JointList.Length - 1]);

                    Debug.Log(string.Format("[TRF] {0} - lastPart " + lastPart.name, 40));

                    // Add EndEffector/LEE/ (last part) to SortIkServo - problem -> if LEE hold other element that will last part
                    SortIkServo.Add(new IKservo(lastPart));

                    // if have all IKservo than calculate IK parameters of IKservos 
                    if (SortIkServo.Count == JointList.Length)
                    {
                        partPosition = part.transform.position;
                        partRotation = part.transform.rotation;

                        // dumping servo structure
                        DumpServoStructure(SortIkServo);

                        SetFKParamsToBuffer();
                        OnSave(null);
                        Debug.Log(string.Format("[TRF] {0} Servo Structure Dumped", 50));
                        MsgWindow(new Vector2(1920f / 2f, 1080f / 2f), "Servo dumping message", "Servo Structure Dumped");
                    }
                    else
                        MsgWindow(new Vector2(1920f / 2f, 1080f / 2f), "Servo dumping message", "Servo Structure Not Dumped");

                }
            }
            else
                return;

            if (GetFKParamsFromBuffer())
            {
                IsInitedModule = true;
                Debug.Log(string.Format("[TRF] Start() - IsInitedModule = true"));
            }
        }

        [KSPEvent(guiActive = true, guiName = "Turn on IKRC", active = false)]
        public void TurnOnIKRCEvent()
        {
            if (IsInitedModule)
            {
                GetWinParamsFromBuffer();
                // This will hide the TurnOnIKRCEvent event, and show the TurnOffIKRCEvene event.
                Events["TurnOnIKRCEvent"].active = false;
                Events["TurnOffIKRCEvent"].active = true;

                // cPos - tartget position = current arm position
                globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;

                ircWindowActive = true;
            }
        }

        [KSPEvent(guiActive = true, guiName = "Turn off IKRC", active = false)]
        public void TurnOffIKRCEvent()
        {
            // This will hide the TurnOffIKRCEven event, and show the TurnOnIKRCEvent event.
            Events["TurnOnIKRCEvent"].active = true;
            Events["TurnOffIKRCEvent"].active = false;
            ircWindowActive = false;
            //Debug.Log("[TRF] TurnOffIKRCEvent()}");
            SetWinParamsToBuffer();
            OnSave(nodeInner);
        }
        #endregion KSPEvents

        #region OnMethods
        public void OnRenderObject()
        {
            if (ircWindowActive && HighLogic.LoadedSceneIsFlight && IsInitedModule)
            {
                if (targetIconHas)
                {
                    if (SortIkServo[SortIkServo.Count - 1] != null)
                    {
                        DrawTools.DrawSphere(SortIkServo[SortIkServo.Count - 1].ServoTransform.position, Color.white, 0.1f);
                    }

                    if (dockingNodeObject != null)
                    {
                        //DrawTools.DrawTransform(dockingNodeObject, 0.3f);
                        DrawTools.DrawSphere(dockingNodeObject.position, Color.yellow, 0.1f);

                        //if (global != null)
                        //{
                        //    //DrawTools.DrawTransform(global.transform, 0.2f);
                        //    DrawTools.DrawSphere(global.transform.position, Color.red, 0.075f);
                        //}

                        //DrawTools.DrawTransform(SortIkServo[SortIkServo.Count - 1].ServoTransform, 0.3f);

                        if (global != null)
                        {
                            //DrawTools.DrawTransform(global.transform, 0.2f);
                            DrawTools.DrawSphere(global.transform.position, Color.red, 0.05f);
                        }
                    }
                }

                if (servoTransformHas)
                {
                    //Debug.Log(string.Format("[TRF] {0} - !IK_active ", 102));
                    //for (int i = 0; i < (servoGimbal.Length - 1); i++)
                    for (int i = 0; i < servoGimbal.Length; i++)
                    {
                        DrawTools.DrawTransform(servoGimbal[i].transform, 0.3f);
                        //Debug.Log(string.Format("[TRF] {0} - DrawTransform[{1}]", 103, i));
                    }

                    //if (hoverObject != null)
                    //    DrawTools.DrawTransform(hoverObject.transform, 0.3f);

                    if (dockingNodeObject != null)
                    {
                        DrawTools.DrawTransform(dockingNodeObject, 0.3f);
                        //DrawTools.DrawSphere(dockingNodeObject.position, Color.yellow, 0.1f);
                    }

                    //DrawTools.DrawTransform(SortIkServo[SortIkServo.Count - 1].ServoTransform, 0.3f);
                    //DrawTools.DrawSphere(SortIkServo[SortIkServo.Count - 1].ServoTransform.position, Color.white, 0.1f);

                    if (global != null)
                    {
                        DrawTools.DrawTransform(global.transform, 0.2f);
                        //DrawTools.DrawSphere(global.transform.position, Color.red, 0.05f);
                    }
                }
            }
        }

        public override void OnSave(ConfigNode node)
        {
            if ((HighLogic.LoadedSceneIsFlight && IsInitedModule) || HighLogic.LoadedSceneIsEditor)
            {
                PluginConfiguration config = PluginConfiguration.CreateForType<IkRobotController>();

                // save window parameters from buffer to config.xml
                config.SetValue("IRC Window Position", windowPosition);
                Debug.Log(string.Format("[TRF] {0} - OnSave() windowPosition {1}, {2}", 0, windowPosition.x, windowPosition.y));

                // save FK parameters from buffer to config.xml
                if (FKparamsIkServo != null)
                {
                    if (FKparamsIkServo.Count == 8)
                    {
                        int servoNumber = 0;
                        foreach (IKservo ikServo in FKparamsIkServo)
                        {
                            config.SetValue(servoNumber.ToString() + "-Servo-Params-PartRotation", ikServo.fkParams.PartRotation);
                            config.SetValue(servoNumber.ToString() + "-Servo-Params-ParentOffset", ikServo.fkParams.ParentOffset);
                            config.SetValue(servoNumber.ToString() + "-Servo-Params-Axis", ikServo.fkParams.Axis);
                            //config.SetValue(servoNumber.ToString() + "-Servo-Params-Position", ikServo.fkParams.Position);
                            config.SetValue(servoNumber.ToString() + "-Servo-Params-Rotation", ikServo.fkParams.Rotation);

                            servoNumber++;
                        }

                        config.save();

                        Debug.Log(string.Format("[TRF] {0} end of OnSave()", 0));
                    }
                }
            }

            Debug.Log(string.Format("[TRF] - OnSave() " + HighLogic.LoadedScene.ToString()));
            Debug.Log(string.Format("[TRF] - OnSave() {0}", onSaveCounter));
            onSaveCounter++;
        }

        public override void OnLoad(ConfigNode node)
        {
            Debug.Log(string.Format("[TRF] {0} Start of OnLoad()", 0));

            //if (HighLogic.LoadedSceneIsFlight /*|| HighLogic.LoadedSceneIsEditor*/)
            //{
            PluginConfiguration config = PluginConfiguration.CreateForType<IkRobotController>();

            config.load();

            // load window parameters from config.xml to buffer
            windowPosition = config.GetValue<Vector2>("IRC Window Position");
            Debug.Log(string.Format("[TRF] {0} - OnLoad() windowPosition {1}, {2}", 42, windowPosition.x, windowPosition.y));

            if (FKparamsIkServo == null)
                CreateFKparams();

            // load FK parameters from config.xml to buffer
            int servoNumber = 0;
            foreach (IKservo ikServo in FKparamsIkServo)
            {
                ikServo.fkParams.PartRotation = config.GetValue<Quaternion>(servoNumber.ToString() + "-Servo-Params-PartRotation");
                ikServo.fkParams.ParentOffset = config.GetValue<Vector3>(servoNumber.ToString() + "-Servo-Params-ParentOffset");
                ikServo.fkParams.Axis = config.GetValue<Vector3>(servoNumber.ToString() + "-Servo-Params-Axis");
                //ikServo.fkParams.Position = config.GetValue<Vector3>(servoNumber.ToString() + "-Servo-Params-Position");
                ikServo.fkParams.Rotation = config.GetValue<Quaternion>(servoNumber.ToString() + "-Servo-Params-Rotation");

                Debug.Log(string.Format("[TRF] {0} - OnLoad() " + VectorToString(ikServo.fkParams.ParentOffset, "0.00"), 42));

                servoNumber++;
            }

            GetWinParamsFromBuffer();
            ircWindowActive = false;
            nodeInner = node;
            //}

            Debug.Log(string.Format("[TRF] {0} end of OnLoad()", 0));

            Debug.Log(string.Format("[TRF] - OnLoad() " + HighLogic.LoadedScene.ToString()));
            Debug.Log(string.Format("[TRF] - OnLoad() {0}", onLoadCounter));
            onLoadCounter++;

            foreach (IKservo ikServo in FKparamsIkServo)
            {
                if (ikServo.fkParams.ParentOffset != (new Vector3(0f, 0f, 0f)))
                {
                    IsLoadedFkParams = true;
                    return;
                }
            }
        }

        private void SetWinParamsToBuffer()
        {
            windowPosition.x = windowRectangle.x;
            windowPosition.y = windowRectangle.y;
        }

        private void GetWinParamsFromBuffer()
        {
            if (windowRectangle != null)
                windowRectangle = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
        }

        private void SetFKParamsToBuffer()
        {
            int servoNumber = 0;
            foreach (IKservo ikServo in FKparamsIkServo)
            {
                ikServo.fkParams.PartRotation = SortIkServo[servoNumber].fkParams.PartRotation;
                ikServo.fkParams.ParentOffset = SortIkServo[servoNumber].fkParams.ParentOffset;
                ikServo.fkParams.Axis = SortIkServo[servoNumber].fkParams.Axis;
                //ikServo.fkParams.Position = SortIkServo[servoNumber].fkParams.Position;
                ikServo.fkParams.Rotation = SortIkServo[servoNumber].fkParams.Rotation;

                servoNumber++;
            }

            IsLoadedFkParams = true;
        }

        private bool GetFKParamsFromBuffer()
        {
            if (IsLoadedFkParams)
            {
                Debug.Log(string.Format("[TRF] {0} - GetFKParamsFromBuffer()", 42));
                Debug.Log(string.Format("[TRF] {0} - part.transform.rotation " + QuaternionToString(part.transform.rotation, "0.00"), 42));
                int servoNumber = 0;
                foreach (IKservo ikServo in FKparamsIkServo)
                {
                    SortIkServo[servoNumber].fkParams.PartRotation = ikServo.fkParams.PartRotation;
                    SortIkServo[servoNumber].fkParams.ParentOffset = ikServo.fkParams.ParentOffset;
                    SortIkServo[servoNumber].fkParams.Axis = ikServo.fkParams.Axis;
                    SortIkServo[servoNumber].fkParams.Rotation = ikServo.fkParams.Rotation;

                    Debug.Log(string.Format("[TRF] {0} - ParentOffset " + VectorToString(SortIkServo[servoNumber].fkParams.ParentOffset, "0.00"), 42));
                    Debug.Log(string.Format("[TRF] {0} - Axis " + VectorToString(ikServo.fkParams.Axis, "0.0"), 40));
                    Debug.Log(string.Format("[TRF] {0} - Rotation " + QuaternionToString(SortIkServo[servoNumber].fkParams.Rotation, "0.00"), 42));

                    servoNumber++;
                }

                partRotation = SortIkServo[0].fkParams.PartRotation;
            }
            else
                return false;

            return true;
        }

        public void OnDisable()
        {
            //if (ircWindowActive)
            //{
            //    Debug.Log("[TRF] - OnDisable()");
            //    ircWindowActive = false;
            //    OnSave(nodeInner);
            //}
            ircWindowActive = false;
            IK_active = false;
            servoTransformHas = false;
            targetIconHas = false;
            IsInitedModule = false;
        }

        public void OnDestroy()
        {
            if (ircWindowActive)
            {
                Debug.Log("[TRF] - Destroy()");
                SetWinParamsToBuffer();
                ircWindowActive = false;
                OnSave(nodeInner);
            }
            IK_active = false;
            servoTransformHas = false;
            targetIconHas = false;
            IsInitedModule = false;
        }

        public void Destroy()
        {
            if (ircWindowActive)
            {
                Debug.Log("[TRF] - Destroy()");
                SetWinParamsToBuffer();
                ircWindowActive = false;
                OnSave(nodeInner);
            }
            IK_active = false;
            servoTransformHas = false;
            targetIconHas = false;
            IsInitedModule = false;
        }
        #endregion OnMethods

        #region GUI
        public bool ButtonsIsReleased()
        {
            if ((buttons.Translation == new Vector3(0f, 0f, 0f)) && (buttons.Rotation.eulerAngles == new Vector3(0f, 0f, 0f)))
                return true;
            else
                return false;
        }

        public void ButtonsReleased()
        {
            buttons.Translation = new Vector3(0f, 0f, 0f);
            buttons.Rotation = Quaternion.identity;
        }

        public void OnGUI()
        {
            if (ircWindowActive)
            {
                // Draw window
                windowRectangle = GUILayout.Window(WindowID, windowRectangle, OnWindow, "IK Robot Controller");
                //Debug.Log(string.Format("[TRF{1}] {0} ircWindowActive", 1000, WindowID));
            }

            if (msgWindowActive)
            {
                // Draw message window
                msgWindowRectangle = GUILayout.Window(MsgWindowID, msgWindowRectangle, OnMsgWindow, msgWindowTitle);
            }
        }

        public void OnWindow(int windowID)
        {
            if (IsInitedModule)
            {
                GUILayout.BeginHorizontal();

                #region Translation & Rotation Buttons
                // Coloring translation and rotation buttons
                if (buttonIsColored)
                {
                    InitStyles();
                    // Translation buttons
                    GUI.Box(new Rect(trButtonsOrigin.x - 2, trButtonsOrigin.y + 33, 34, 34), "", greenStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 68, trButtonsOrigin.y + 33, 34, 34), "", greenStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 33, trButtonsOrigin.y - 2, 34, 34), "", redStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 33, trButtonsOrigin.y + 68, 34, 34), "", redStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x - 2, trButtonsOrigin.y + 68, 34, 34), "", blueStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 68, trButtonsOrigin.y - 2, 34, 34), "", blueStyle);
                    // Rotation buttons
                    GUI.Box(new Rect(trButtonsOrigin.x + 108, trButtonsOrigin.y + 33, 34, 34), "", blueStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 178, trButtonsOrigin.y + 33, 34, 34), "", blueStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 143, trButtonsOrigin.y - 2, 34, 34), "", greenStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 143, trButtonsOrigin.y + 68, 34, 34), "", greenStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 108, trButtonsOrigin.y + 68, 34, 34), "", redStyle);
                    GUI.Box(new Rect(trButtonsOrigin.x + 178, trButtonsOrigin.y - 2, 34, 34), "", redStyle);
                }

                // Translation buttons
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x, trButtonsOrigin.y + 35, 30, 30), "◄"))
                    buttons.Translation.y = 0.025f * transStep;
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 70, trButtonsOrigin.y + 35, 30, 30), "►"))
                    buttons.Translation.y = -0.025f * transStep;
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 35, trButtonsOrigin.y, 30, 30), "▲"))
                    buttons.Translation.x = 0.025f * transStep;
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 35, trButtonsOrigin.y + 70, 30, 30), "▼"))
                    buttons.Translation.x = -0.025f * transStep;
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x, trButtonsOrigin.y + 70, 30, 30), "●"))
                    buttons.Translation.z = -0.025f * transStep;
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 70, trButtonsOrigin.y, 30, 30), "•"))
                    buttons.Translation.z = 0.025f * transStep;

                // Rotation buttons

                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 110, trButtonsOrigin.y + 35, 30, 30), "˅ʘ"))
                    buttons.Rotation = Quaternion.Euler(0f, 0f, 0.5f * rotStep);
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 180, trButtonsOrigin.y + 35, 30, 30), "ʘ˅"))
                    buttons.Rotation = Quaternion.Euler(0f, 0f, -0.5f * rotStep);
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 145, trButtonsOrigin.y, 30, 30), "˄"))
                    buttons.Rotation = Quaternion.Euler(0f, 0.5f * rotStep, 0f);
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 145, trButtonsOrigin.y + 70, 30, 30), "˅"))
                    buttons.Rotation = Quaternion.Euler(0f, -0.5f * rotStep, 0f);
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 110, trButtonsOrigin.y + 70, 30, 30), "˂"))
                    buttons.Rotation = Quaternion.Euler(-0.5f * rotStep, 0f, 0f);
                if (GUI.RepeatButton(new Rect(trButtonsOrigin.x + 180, trButtonsOrigin.y, 30, 30), "˃"))
                    buttons.Rotation = Quaternion.Euler(0.5f * rotStep, 0f, 0f);
                #endregion Translation & Rotation Buttons

                // IK active toggle
                IK_active = GUI.Toggle(new Rect(15, 15, 70, 20), IK_active, " IK active");
                // icon of target toggle
                targetIconHas = GUI.Toggle(new Rect(90, 15, 60, 20), targetIconHas, " trgICO");
                // transform of servos toggle
                servoTransformHas = GUI.Toggle(new Rect(150, 15, 75, 20), servoTransformHas, " servoTRF");
                // zeroTheta iterate toggle
                zeroThetaIterate = GUI.Toggle(new Rect(230, 15, 60, 20), zeroThetaIterate, " 0ƟIter");
                // clear values of Theta
                clearThetasBool = GUI.Button(new Rect(250, 35, 45, 20), "clrƟ");
                // set aim position and orientation to actual state
                actualStateBool = GUI.Button(new Rect(250, 60, 45, 20), "acSt");
                // set base position and orientation
                baseStateBool = GUI.Button(new Rect(250, 85, 45, 20), "0St");

                // reset position and rotation of endeffector
                if (GUI.Button(new Rect(250, 110, 45, 20), "origin"))
                {
                    globalPosition = baseState.Translation;
                    globalQuaternion = baseState.Rotation;
                }

                // set current position and rotation of endeffector
                if (GUI.Button(new Rect(250, 135, 45, 20), "cPos"))
                {
                    globalPosition = SortIkServo[SortIkServo.Count - 1].ServoTransform.position;
                    globalQuaternion = SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation;
                }

                #region Target Definition
                // set selected part than virtual endeffector
                veeStateBool = GUI.Button(new Rect(10, 210, 40, 20), "VEE");
                // set aim position and orientation to selected PDGF
                pdgfStateBool = GUI.Button(new Rect(60, 210, 50, 20), "target");
                // set selected position and orientation than near aim
                nearPDGFBool = GUI.Toggle(new Rect(120, 210, 70, 20), nearPDGFBool, " dockPos");
                // set refresh selected position and orientation
                refreshPDGFBool = GUI.Toggle(new Rect(200, 210, 70, 20), refreshPDGFBool, " rePos");

                // Force Distance and ForceRoll
                Yoffset = 0;
                inputRect = new Rect(10, 235, 40, 20);
                // Force DistZ
                IsForceDist = GUI.Toggle(new Rect(10, 235, 90, 20), IsForceDist, " Force DistZ");
                AddInputValue(inputRect, forceDistStringZ, out forceDistStringZ, "", out forceDistNumberZ, 90f);
                // Force RollZ
                IsForceRoll = GUI.Toggle(new Rect(10, 255, 90, 20), IsForceRoll, " Force RollZ");
                AddInputValue(inputRect, forceRollStringZ, out forceRollStringZ, "", out forceRollNumberZ, 90f);

                Yoffset = 0;
                inputRect = new Rect(140, 235, 40, 20);
                // Force DistX
                AddInputValue(inputRect, forceDistStringX, out forceDistStringX, "DistX", out forceDistNumberX, 35f);
                // Force RollX
                AddInputValue(inputRect, forceRollStringX, out forceRollStringX, "RollX", out forceRollNumberX, 35f);

                Yoffset = 0;
                inputRect = new Rect(225, 235, 40, 20);
                // Force DistZ
                AddInputValue(inputRect, forceDistStringY, out forceDistStringY, "DistY", out forceDistNumberY, 35f);
                // Force RollZ
                AddInputValue(inputRect, forceRollStringY, out forceRollStringY, "RollY", out forceRollNumberY, 35f);
                #endregion Target Definition

                #region close window
                // Close window button
                if (GUI.Button(new Rect(closeWindowButtonPosition.x, closeWindowButtonPosition.y, 17, 15), "x"))
                {
                    ircWindowActive = false;
                    // This will hide the TurnOffIKRCEven event, and show the TurnOnIKRCEvent event.
                    Events["TurnOnIKRCEvent"].active = true;
                    Events["TurnOffIKRCEvent"].active = false;
                    SetWinParamsToBuffer();
                    ircWindowActive = false;
                    Debug.Log("[TRF] Close window button");
                    OnSave(nodeInner);
                }
                //Debug.Log(string.Format("[TRF] {0} - Close window button", 1004));
                #endregion close window

                #region window extender
                // Extend windows button
                if (!WindowIsExtended)
                {
                    if (GUI.Button(new Rect(3, 3, 17, 15), ">"))
                    {
                        WindowIsExtended = true;
                        windowSize = new Vector2(520, 600);
                        if (windowRectangle != null)
                            //windowRectangle = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
                            windowRectangle = new Rect(windowRectangle.x, windowRectangle.y, windowSize.x, windowSize.y);
                        closeWindowButtonPosition = new Vector2(windowSize.x - 20, 3);
                    }
                }
                else
                {
                    if (GUI.Button(new Rect(3, 3, 17, 15), "<"))
                    {
                        WindowIsExtended = false;
                        windowSize = new Vector2(310, 600);
                        if (windowRectangle != null)
                            //windowRectangle = new Rect(windowPosition.x, windowPosition.y, windowSize.x, windowSize.y);
                            windowRectangle = new Rect(windowRectangle.x, windowRectangle.y, windowSize.x, windowSize.y);
                        closeWindowButtonPosition = new Vector2(windowSize.x - 20, 3);
                    }

                    #region IK display of parameters
                    // IK values
                    Yoffset = 0;
                    inputRect = new Rect(320, 395, 40, 20);
                    AddInputValue(inputRect, iterateString, out iterateString, "IK_iterate", out iterateNumber, 150f);
                    AddInputValue(inputRect, samplingAngleString, out samplingAngleString, "IK_samplingAngle", out SamplingAngle, 150f);
                    AddInputValue(inputRect, DistanceThresholdString, out DistanceThresholdString, "IK_DistanceThreshold", out DistanceThreshold, 150f);
                    AddOutputValue(inputRect, "IK_Distance", Distance, 150f);
                    AddInputValue(inputRect, AngleThresholdString, out AngleThresholdString, "IK_AngleThreshold", out AngleThreshold, 150f);
                    AddOutputValue(inputRect, "IK_Angle", Angle, 150f);
                    AddInputValue(inputRect, MaxPosErrString, out MaxPosErrString, "IK_MaxPosErr", out MaxPosErr, 150f);
                    AddInputValue(inputRect, MaxOriErrString, out MaxOriErrString, "IK_MaxOriErr", out MaxOriErr, 150f);
                    AddOutputValue(inputRect, "IK_DinLearningRatePos", DinLearningRatePos, 150f);
                    AddOutputValue(inputRect, "IK_DinLearningRateOri", DinLearningRateOri, 150f);
                    #endregion IK display of parameters
                }
                #endregion window extender

                #region Theta values
                // Theta values
                Yoffset = 0;
                inputRect = new Rect(10, 365, 50, 20);
                for (int i = 0; i < 7; i++)
                {
                    if (IK_active)
                        AddOutputValue(inputRect, "Ɵ[" + i.ToString() + "]", theta[i], 30f);
                    else
                    {
                        thetaString[i] = theta[i].ToString("0.000");
                        AddInputValue(inputRect, thetaString[i], out thetaString[i], "Ɵ[" + i.ToString() + "]", out theta[i], 30f);
                    }
                }

                // Theta current values
                Yoffset = 0;
                inputRect = new Rect(110, 365, 50, 20);
                for (int i = 0; i < 7; i++)
                {
                    AddOutputValue(inputRect, "Ɵ[" + i.ToString() + "]", currentTheta[i], 30f);
                }

                //Theta blocker
                blocking[0] = GUI.Toggle(new Rect(92, 365, 20, 20), blocking[0], "");
                blocking[1] = GUI.Toggle(new Rect(92, 385, 20, 20), blocking[1], "");
                blocking[2] = GUI.Toggle(new Rect(92, 405, 20, 20), blocking[2], "");
                blocking[3] = GUI.Toggle(new Rect(92, 425, 20, 20), blocking[3], "");
                blocking[4] = GUI.Toggle(new Rect(92, 445, 20, 20), blocking[4], "");
                blocking[5] = GUI.Toggle(new Rect(92, 465, 20, 20), blocking[5], "");
                blocking[6] = GUI.Toggle(new Rect(92, 485, 20, 20), blocking[6], "");
                #endregion Theta values

                #region position information
                Yoffset = 0;
                inputRect = new Rect(10, 280, 40, 20);
                AddOutputValue(inputRect, "IK_Position", globalPosition, 80f, 90f);
                AddOutputValue(inputRect, "IK_Rotation", globalQuaternion.eulerAngles, 80f, 90f);
                AddOutputValue(inputRect, "EE_Position", SortIkServo[SortIkServo.Count - 1].ServoTransform.position, 80f, 90f);
                AddOutputValue(inputRect, "EE_Rotation", SortIkServo[SortIkServo.Count - 1].ServoTransform.rotation.eulerAngles, 80f, 90f);
                inputRect = new Rect(200, 280, 40, 20);
                AddOutputValue(inputRect, "distance", distance, 55f, 45f);
                AddOutputValue(inputRect, "angle", angle, 55f, 45f);
                AddOutputValue(inputRect, "wDist", workingDistance, 55f, 45f);
                AddInputValue(inputRect, ArmMaxLengthString, out ArmMaxLengthString, "wRange", out ArmMaxLength, 55f);
                #endregion position information

                // emergency servo stop
                emergencyServoStop = GUI.Toggle(new Rect(225, 280, 80, 20), emergencyServoStop, " EMGstp");

                // Success value
                GUI.Toggle(new Rect(225, 300, 80, 20), Success, " Success");
                // IK_Success value
                GUI.Toggle(new Rect(225, 320, 80, 20), IKsuccess, " IK Success");
                // Bug value
                GUI.Toggle(new Rect(225, 340, 80, 20), Bug, " Bug");

                // slider of servo speed
                float[] speedSliderValues = { 0.0f, 0.031f, 0.063f, 0.125f, 0.25f, 0.5f, 1.0f, 2.0f, 4.0f };
                servoSpeed = FixValuesLogSlider(new Rect(10, 140, 150, 20), 50f, "speed", servoSpeed, speedSliderValues);
                // slider of control button translation's step
                float[] transSliderValues = { 0.0f, 0.0375f, 0.075f, 0.125f, 0.25f, 0.5f, 1.0f, 2.0f };
                transStep = FixValuesLogSlider(new Rect(10, 160, 150, 20), 50f, "TRLstp", transStep, transSliderValues);
                // slider of control button rotation's step
                float[] rotSliderValues = { 0.0f, 0.0375f, 0.075f, 0.125f, 0.25f, 0.5f, 1.0f, 2.0f };
                rotStep = FixValuesLogSlider(new Rect(10, 180, 150, 20), 50f, "ROTstp", rotStep, rotSliderValues);

                // set position and rotation of endeffector ???

                Color[] color = { Color.red, Color.yellow };
                LineDiagram(diagramRectangle, data, color);

                GUILayout.EndHorizontal();

                GUI.DragWindow();
            }
        }

        public void OnMsgWindow(int msgWindowID)
        {
            GUILayout.BeginHorizontal();

            GUI.Label(new Rect(30, msgWindowRectangle.height / 2 - 15, msgWindowRectangle.width - 60, 20), msgText);

            // Close window button
            if (GUI.Button(new Rect(180, 3, 17, 15), "x"))
            {
                msgWindowActive = false;
            }
            // set current position and rotation of endeffector
            if (GUI.Button(new Rect((200 - 45) / 2, 70, 45, 25), "OK"))
            {
                msgWindowActive = false;
            }

            GUILayout.EndHorizontal();

            GUI.DragWindow();
        }

        public void MsgWindow(Vector2 pos, string title, string text)
        {
            msgWindowPosition = pos;
            msgWindowTitle = title;
            msgText = text;

            msgWindowActive = true;
        }

        private void InitStyles()
        {
            if (redStyle == null)
            {
                redStyle = new GUIStyle(GUI.skin.box);
                redStyle.normal.background = MakeTexWithRound(34, 34, colorControlButtonUpDown);
            }
            if (greenStyle == null)
            {
                greenStyle = new GUIStyle(GUI.skin.box);
                greenStyle.normal.background = MakeTexWithRound(34, 34, colorControlButtonLeftRight);
            }
            if (blueStyle == null)
            {
                blueStyle = new GUIStyle(GUI.skin.box);
                blueStyle.normal.background = MakeTexWithRound(34, 34, colorControlButtonForwardBackward);
            }
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        private Texture2D MakeTexWithRound(int width, int height, Color col)
        {
            Color transColor = new Color(col.r, col.g, col.b, 0f);
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i)
            {
                int x = i % width;
                int y = i / width;
                //Debug.Log("i = " + i + " x = " + x + " y = " + y);
                if (x == 0 || x == (width - 1))
                {
                    if (y == 0 || y == (height - 1) || y == 1 || y == (height - 2))
                    {
                        pix[i] = transColor;
                    }
                    else
                        pix[i] = col;
                }
                else if (x == 1 || x == (width - 2))
                {
                    if (y == 0 || y == (height - 1))
                    {
                        pix[i] = transColor;
                    }
                    else
                        pix[i] = col;
                }
                else
                    pix[i] = col;
            }
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public void AddInputValue(Rect rectangle, string valueInString, out string valueOutString, string name, out int valueInt, float labelLength)
        {
            GUI.Label(new Rect(rectangle.x, rectangle.y + Yoffset, labelLength, rectangle.height), name);
            valueOutString = GUI.TextField(new Rect(rectangle.x + labelLength, rectangle.y + Yoffset, rectangle.width, rectangle.height), valueInString);
            try
            {
                valueInt = int.Parse(valueOutString);
            }
            catch
            {
                valueOutString = valueInString;
                valueInt = int.Parse(valueOutString);
            }
            Yoffset += 20f;
        }

        public void AddInputValue(Rect rectangle, string valueInString, out string valueOutString, string name, out float valueFloat, float labelLength)
        {
            GUI.Label(new Rect(rectangle.x, rectangle.y + Yoffset, labelLength, rectangle.height), name);
            valueOutString = GUI.TextField(new Rect(rectangle.x + labelLength, rectangle.y + Yoffset, rectangle.width, rectangle.height), valueInString);
            try
            {
                valueFloat = float.Parse(valueOutString);
            }
            catch
            {
                valueOutString = valueInString;
                valueFloat = float.Parse(valueOutString);
            }
            Yoffset += 20f;
        }

        public void AddOutputValue(Rect rectangle, string name, float valueFloat, float labelLength)
        {
            GUI.Label(new Rect(rectangle.x, rectangle.y + Yoffset, labelLength, rectangle.height), name);
            GUI.TextField(new Rect(rectangle.x + labelLength, rectangle.y + Yoffset, rectangle.width, rectangle.height), valueFloat.ToString("0.000"));
            Yoffset += 20f;
        }

        public void AddOutputValue(Rect rectangle, string name, float valueFloat, float labelLength, float fieldLength)
        {
            GUI.Label(new Rect(rectangle.x, rectangle.y + Yoffset, labelLength, rectangle.height), name);
            GUI.TextField(new Rect(rectangle.x + labelLength, rectangle.y + Yoffset, fieldLength, rectangle.height), valueFloat.ToString("0.000"));
            Yoffset += 20f;
        }

        private void AddOutputValue(Rect rectangle, string name, Vector3 valueVector3, float labelLength, float fieldLength)
        {
            GUI.Label(new Rect(rectangle.x, rectangle.y + Yoffset, labelLength, rectangle.height), name);
            GUI.TextField(new Rect(rectangle.x + labelLength, rectangle.y + Yoffset, rectangle.width + fieldLength, rectangle.height), VectorToString(valueVector3, "0.00"));
            Yoffset += 20f;
        }

        private float FixValuesSlider(Rect rectangle, float nameLenght, string name, float value, float[] Values)
        {
            float mindiff = Values[Values.Length - 1] - Values[0];
            int index = 0;
            Rect valueRect = rectangle;
            valueRect.x += valueRect.width + nameLenght + 5;
            GUI.Label(rectangle, name);
            GUI.Label(valueRect, value.ToString());
            rectangle.x += nameLenght;
            rectangle.y += 7;
            value = GUI.HorizontalSlider(rectangle, value, Values[0], Values[Values.Length - 1]);
            for (int i = 0; i < Values.Length; i++)
            {
                if (Math.Abs(value - Values[i]) < mindiff)
                {
                    mindiff = Math.Abs(value - Values[i]);
                    index = i;
                }
            }
            value = Values[index];

            return value;
        }

        private float FixValuesLogSlider(Rect rectangle, float nameLenght, string name, float value, float[] Values)
        {
            float[] LogValues = new float[Values.Length];

            for (int i = 0; i < Values.Length; i++)
            {
                if (i == 0)
                    LogValues[0] = Mathf.Log10((Values[1] / 2f));
                else
                    LogValues[i] = Mathf.Log10(Values[i]);
            }

            float mindiff = Values[Values.Length - 1] - Values[0];
            int index = 0;
            Rect valueRect = rectangle;

            valueRect.x += valueRect.width + nameLenght + 5f;
            GUI.Label(rectangle, name);
            GUI.Label(valueRect, value.ToString());
            rectangle.x += nameLenght;

            Rect fixLine = new Rect(rectangle);
            fixLine.width = 10f;
            fixLine.y += 2f;
            Rect fixLineRect = new Rect(fixLine);

            for (int i = 0; i < LogValues.Length; i++)
            {
                float linePosition = (rectangle.width - 12f) / Mathf.Abs(LogValues[0] - LogValues[LogValues.Length - 1]) * (LogValues[i] + LogValues[0] * -1f) + 2f;
                fixLineRect.x = fixLine.x + linePosition;
                GUI.Label(fixLineRect, "│");
            }

            rectangle.y += 6f;
            float valueLog;
            if (value == 0f)
                valueLog = LogValues[0];
            else
                valueLog = Mathf.Log10(value);

            valueLog = GUI.HorizontalSlider(rectangle, valueLog, LogValues[0], LogValues[Values.Length - 1]);
            value = (float)Math.Pow(10d, (double)valueLog);
            for (int i = 0; i < Values.Length; i++)
            {
                if (Math.Abs(value - Values[i]) < mindiff)
                {
                    mindiff = Math.Abs(value - Values[i]);
                    index = i;
                }
            }
            if (index == 0)
                value = 0f;
            else
                value = Values[index];

            return value;
        }

        private void DrawRectangle(Rect rectangle, Color color)
        {
            Drawing.DrawLine(new Vector2(rectangle.x, rectangle.y), new Vector2(rectangle.x + rectangle.width, rectangle.y), color);
            Drawing.DrawLine(new Vector2(rectangle.x, rectangle.y), new Vector2(rectangle.x, rectangle.y + rectangle.height), color);
            Drawing.DrawLine(new Vector2(rectangle.x + rectangle.width, rectangle.y), new Vector2(rectangle.x + rectangle.width, rectangle.y + rectangle.height), color);
            Drawing.DrawLine(new Vector2(rectangle.x, rectangle.y + rectangle.height), new Vector2(rectangle.x + rectangle.width, rectangle.y + rectangle.height), color);
        }

        private void LineDiagram(Rect rectangle, List<float> data, Color color)
        {
            // set border deathzone
            Vector2 border = new Vector2(5, 5);
            Rect realDataField = new Rect(new Vector2(rectangle.x + border.x, rectangle.y + border.y), new Vector2(rectangle.width - (2 * border.x), rectangle.height - (2 * border.y)));
            Rect activeDataField = new Rect(new Vector2(0, 0), new Vector2(rectangle.width - (2 * border.x), rectangle.height - (2 * border.y)));
            // draw border line
            DrawRectangle(rectangle, Color.white);

            //DrawRectangle (realDataField, Color.yellow);

            GUI.BeginGroup(realDataField);
            //GUILayout.BeginArea(realDataField);

            // draw diagram data
            if (data.Count > 1)
            {
                for (int i = 1; i < data.Count; i++)
                {
                    Drawing.DrawLine(new Vector2((float)(i - 1), activeDataField.height - data[i - 1]), new Vector2((float)(i), activeDataField.height - data[i]), color);
                }
            }

            GUI.EndGroup();
            //GUILayout.EndArea();
        }

        private void LineDiagram(Rect rectangle, List<List<float>> data, Color[] color)
        {
            // set border deathzone
            Vector2 border = new Vector2(5, 5);
            Rect realDataField = new Rect(new Vector2(rectangle.x + border.x, rectangle.y + border.y), new Vector2(rectangle.width - (2 * border.x), rectangle.height - (2 * border.y)));
            Rect activeDataField = new Rect(new Vector2(0, 0), new Vector2(rectangle.width - (2 * border.x), rectangle.height - (2 * border.y)));
            // draw border line
            DrawRectangle(rectangle, Color.white);

            //DrawRectangle (realDataField, Color.yellow);

            GUI.BeginGroup(realDataField);
            //GUILayout.BeginArea(realDataField);

            if (data.Count > 0)
                for (int j = 0; j < data.Count; j++)
                {
                    // draw diagram data
                    if (data[j].Count > 1)
                    {
                        for (int i = 1; i < data[j].Count; i++)
                        {
                            Drawing.DrawLine(new Vector2((float)(i - 1), activeDataField.height - (data[j][i - 1] + 1)), new Vector2((float)(i), activeDataField.height - (data[j][i] + 1)), color[j]);
                        }
                    }
                }

            GUI.EndGroup();
            //GUILayout.EndArea();
        }


        //static System.Reflection.FieldInfo s_focusedWindow = null;

        //static public int FocusedWindow
        //{
        //    get
        //    {
        //        if (s_focusedWindow == null)
        //        {
        //            s_focusedWindow = typeof(GUI).GetField("focusedWindow", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        //        }

        //        return (int)s_focusedWindow.GetValue(null);
        //    }
        //}

        #endregion GUI

        #region Tools
        private float ArmWorkingDistance()
        {
            float armWorkingDistance;

            armWorkingDistance = Mathf.Abs(Vector3.Distance(globalPosition, part.transform.position));

            return armWorkingDistance;
        }

        private bool TargetInRange(float range)
        {
            float distance = 0f;
            bool InRange = false;

            distance = Mathf.Abs(Vector3.Distance(globalPosition, part.transform.position));
            //Debug.Log(string.Format("[TRF] - Update() - TargetInRange({0})  distance = " + distance.ToString(), range));

            InRange = distance <= range;
            //Debug.Log(string.Format("[TRF] - Update() - TargetInRange({0}) = " + (InRange ? "true" : "false"), range));

            return InRange;
        }

        private Vector3 TargetToRange(float range)
        {
            Vector3 target = globalPosition;
            float scale = 1.0f;

            scale = range / Mathf.Abs(Vector3.Distance(globalPosition, part.transform.position));
            target = Vector3.Lerp(part.transform.position, globalPosition, scale);

            return target;
        }

        private bool ImplementServoRotation(List<IKservo> Servos, float[] theta, float speed = 0.25f)
        {
            bool success = true;
            float servoTheta = 0f;

            for (int j = 0; j < SortIkServo.Count - 1; j++)
                if (!double.IsNaN(theta[j]))
                {
                    // blocking fullrotate of servo
                    if (theta[j] > 180f)
                        servoTheta = theta[j] - 360f;
                    else if (theta[j] < -180f)
                        servoTheta = theta[j] + 360f;
                    else
                        servoTheta = theta[j];

                    SortIkServo[j].iservo.MoveTo(servoTheta, speed);
                }
                else
                    success = false;
            return success;
        }

        private float[] GetActualServoRotation(List<IKservo> Servos)
        {
            float[] currentTheta = new float[8];

            for (int j = 0; j < SortIkServo.Count - 1; j++)
                currentTheta[j] = SortIkServo[j].iservo.Position;

            return currentTheta;
        }

        private void GetChildPartRecursive(Part obj)
        {
            if (null == obj)
                return;

            foreach (Part child in obj.children)
            {
                if (null == child)
                    continue;
                listOfChildrenPart.Add(child);
                GetChildPartRecursive(child);
            }
        }
        #endregion Tools

        #region ..ToSting
        private string VectorToString(Vector3 vector, string format)
        {
            string szoveg = "";

            szoveg = string.Format("( " + vector.x.ToString(format) + ", " + vector.y.ToString(format) + ", " + vector.z.ToString(format) + " )");

            return szoveg;
        }

        private string QuaternionToString(Quaternion quaternion, string format)
        {
            string szoveg = "";

            szoveg = string.Format("( " + quaternion.w.ToString(format) + ", " + quaternion.x.ToString(format) + ", " + quaternion.y.ToString(format) + ", " + quaternion.z.ToString(format) + " )");

            return szoveg;
        }

        private string IKServoToString(IKservo ikServo)
        {
            string szoveg = "";

            szoveg = string.Format("ServoTransform = " + VectorToString(ikServo.ServoTransform.position, "0.00") + " " + QuaternionToString(ikServo.ServoTransform.rotation, "0.00"));

            return szoveg;
        }
        #endregion #region ..ToSting

        #region Kinematics
        public bool InverseKinematics(Vector3 targetPosition, Quaternion targetOrientation, float[] angles)
        {
            // calculate different of distance and -rotation
            LocationRotationDiff locRotDiff = DistanceAndAngleFromTarget(targetPosition, targetOrientation, angles);

            Distance = locRotDiff.LocationDiff;
            Angle = locRotDiff.RotationDiff;

            // if different lower than threshold (distance and rotation) then stop
            if (locRotDiff.LocationDiff < DistanceThreshold && locRotDiff.RotationDiff < AngleThreshold)
                return true;

            for (int i = 0; i < SortIkServo.Count; i++)
            {
                // Gradient descent
                // Update : Solution -= LearningRate * Gradient

                // calculate partial gradient
                LocationRotationDiff gradient = PartialGradient(targetPosition, targetOrientation, angles, i);

                // calculate dinamic learning rate of location different 
                DinLearningRatePos = locRotDiff.LocationDiff / MaxPosErr * 100.0f;
                // calculate dinamic learning rate of rotation different
                DinLearningRateOri = locRotDiff.RotationDiff / MaxOriErr * 100.0f;

                // calculate angle of joint with location gradient and rotation gradient - why sum ???
                angles[i] -= DinLearningRatePos * gradient.LocationDiff + DinLearningRateOri * gradient.RotationDiff;

                // Clamp - limited angle of joint
                angles[i] = Mathf.Clamp(angles[i], SortIkServo[i].MinAngle, SortIkServo[i].MaxAngle);

                // if different lower than threshold (distance and rotation) then stop
                if (locRotDiff.LocationDiff < DistanceThreshold && locRotDiff.RotationDiff < AngleThreshold)
                    return true;
            }
            return false;
        }

        public LocationRotationDiff PartialGradient(Vector3 targetPosition, Quaternion targetOrientation, float[] angles, int i)
        {
            // Saves the angle,
            // it will be restored later
            float angle = angles[i];
            LocationRotationDiff gradient = new LocationRotationDiff();

            // calculate different
            LocationRotationDiff f_xGlobal = DistanceAndAngleFromTarget(targetPosition, targetOrientation, angles);
            // Gradient : [F(x+SamplingDistance) - F(x)] / h

            // change angles with samplingangle (a little bit)
            angles[i] += SamplingAngle;

            // recalculate different
            LocationRotationDiff f_x_plus_dGlobal = DistanceAndAngleFromTarget(targetPosition, targetOrientation, angles);

            // calculate gradient of position
            gradient.LocationDiff = (f_x_plus_dGlobal.LocationDiff - f_xGlobal.LocationDiff) / SamplingAngle;
            // calculate gradient of orientation
            gradient.RotationDiff = (f_x_plus_dGlobal.RotationDiff - f_xGlobal.RotationDiff) / SamplingAngle;

            // Restore angles
            angles[i] = angle;

            return gradient;
        }

        public LocationRotationDiff DistanceAndAngleFromTarget(Vector3 targetPosition, Quaternion targetOrientation, float[] angles)
        {
            LocationRotationDiff different = new LocationRotationDiff();

            VectorSM DistanceAndAngle = ForwardKinematics(angles, SortIkServo);

            different.LocationDiff = Vector3.Distance(DistanceAndAngle.Translation, targetPosition);

            different.RotationDiff = Quaternion.Angle(DistanceAndAngle.Rotation, targetOrientation);

            return different;
        }

        public VectorSM ForwardKinematics(float[] angles, List<IKservo> Servos)
        {
            // Used parameters of servo
            // angles[] - actual angle of servo
            // Servos[].fkParams.Axis - define rotation axis of servo
            // Servos[].fkParams.Rotation - global rotation of servo
            // Servos[].fkParams.ParentOffset - position offset for parentpart

            VectorSM endEffector = new VectorSM();
            // Vector3 prevPoint = Servos[0].Position;
            Vector3 prevPoint = Servos[0].ServoTransform.position;
            //Vector3 prevPoint = part.transform.position;

            // Quaternion rotation = Quaternion.identity;
            // Quaternion rotation = Servos[0].ServoTransform.rotation * Quaternion.Inverse(Servos[0].localRotation);
            // Quaternion rotation = Servos[0].ServoTransform.rotation * Quaternion.Inverse(Servos[0].Rotation);
            Quaternion rotation = part.transform.rotation * Quaternion.Inverse(partRotation);

            for (int i = 1; i < Servos.Count; i++)
            {
                // change rotation by previous servo angle - 'relative change'
                rotation *= Quaternion.AngleAxis(angles[i - 1], Servos[i - 1].fkParams.Axis);
                // change position by ( servo's ParentOffset modified by previous servo angle ) - 'relative change'
                Vector3 nextPoint = prevPoint + rotation * Servos[i - 1].fkParams.Rotation * Servos[i].fkParams.ParentOffset;
                // set position of servotransform's sign
                servoGimbal[i - 1].transform.position = prevPoint;
                // set rotation of servotransform's sign
                servoGimbal[i - 1].transform.rotation = rotation * Servos[i - 1].fkParams.Rotation;

                prevPoint = nextPoint;
            }
            // set endeffector position data
            endEffector.Translation = prevPoint;
            // set endeffector rotation data
            //endEffector.Rotation = rotation * Servos[Servos.Count - 2].fkParams.Rotation;
            endEffector.Rotation = rotation * Servos[Servos.Count - 1].fkParams.Rotation;

            // set position of EEFservotransform's sign
            servoGimbal[Servos.Count - 1].transform.position = endEffector.Translation;
            // set rotation of EEFservotransform's sign
            servoGimbal[Servos.Count - 1].transform.rotation = endEffector.Rotation;

            return endEffector;
        }
        #endregion Kinematics
    }
}
