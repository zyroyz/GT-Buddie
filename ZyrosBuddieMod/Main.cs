using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using GorillaLocomotion;
using BepInEx;

namespace ZyrosBuddie
{
    [BepInPlugin("Zyro.Buddie", "Zyro's Buddie", "0.0.3")]
    public class BuddieBase : BaseUnityPlugin
    {
        // you can freely change the variable below
        private float speed = 1.3f;
        private float recoverTime = 2.0f;
        private float grabRadius = 0.5f;

        // i dont reccomend chaning anything below though

        private List<string> frames = new List<string>();
        private bool isRunning = false;
        private int currentFrame = 0;
        private float nextFrameTime = 0f;

        private GameObject botObject;
        private VRRig botRig;
        private Rigidbody botRb;

        private bool isHeld;
        private bool isFalling;
        private bool isLimp;
        private bool isReturning;
        private float recoverTimer;

        private Vector3 rootPos, headPos, leftHandPos, rightHandPos;
        private Quaternion rootRot, headRot, leftHandRot, rightHandRot;

        void Start()
        {
            StartCoroutine(Initialize());
        }

        IEnumerator Initialize()
        {
            while (GorillaTagger.Instance?.offlineVRRig == null)
            {
                yield return new WaitForSeconds(1.0f);
            }

            CreateBotInstance();
            yield return StartCoroutine(GetScenarioShit());
        }

        IEnumerator GetScenarioShit()
        {
            string[] animUrls = {
                "https://raw.githubusercontent.com/zyroyz/GT-Buddie/refs/heads/main/BotData.txt",
                "https://raw.githubusercontent.com/zyroyz/GT-Buddie/refs/heads/main/BotData2.txt"
            };

            string selectedUrl = animUrls[UnityEngine.Random.Range(0, animUrls.Length)];

            using (UnityWebRequest request = UnityWebRequest.Get(selectedUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string rawData = request.downloadHandler.text;
                    frames = new List<string>(rawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
                    isRunning = true;
                }
            }
        }

        void LateUpdate()
        {
            if (botObject == null) return;

            if (GorillaTagger.Instance.offlineVRRig != null)
            {
                GorillaTagger.Instance.myVRRig.transform.localScale = new Vector3(2f, 2f, 2f); // keep this because it fixes you being small
            }

            HandleGrabbing();

            if (isHeld) return;

            if (isFalling || isLimp)
            {
                PhysicsManager();
                return;
            }

            if (isRunning && frames.Count > 0)
            {
                if (Time.time >= nextFrameTime)
                {
                    ParseFrameData(frames[currentFrame]);
                    currentFrame = (currentFrame + 1) % frames.Count;
                    nextFrameTime = Time.time + 0.0166f;
                }
                UpdateBotTransform();
            }
        }

        void HandleGrabbing()
        {
            Transform leftHand = GorillaTagger.Instance.leftHandTransform;
            Transform rightHand = GorillaTagger.Instance.rightHandTransform;

            float distLeft = Vector3.Distance(leftHand.position, botObject.transform.position);
            float distRight = Vector3.Distance(rightHand.position, botObject.transform.position);

            bool leftGrabbing = ControllerInputPoller.instance.leftGrab && distLeft < grabRadius;
            bool rightGrabbing = ControllerInputPoller.instance.rightGrab && distRight < grabRadius;
            bool isGrabbing = leftGrabbing || rightGrabbing;

            if (isGrabbing)
            {
                isHeld = true;
                isFalling = isLimp = isReturning = false;
                botRb.isKinematic = true;

                Transform activeHand = leftGrabbing ? leftHand : rightHand;
                botObject.transform.position = activeHand.position;
                botObject.transform.rotation = activeHand.rotation;
            }
            else if (isHeld)
            {
                isHeld = false;
                isFalling = true;
                botRb.isKinematic = false;
                botRb.useGravity = true;

                Rigidbody playerRb = GorillaLocomotion.GTPlayer.Instance.GetComponent<Rigidbody>();
                botRb.linearVelocity = playerRb.linearVelocity * 2f;
                botRb.AddTorque(UnityEngine.Random.insideUnitSphere * 15f, ForceMode.Impulse);
            }
        }

        void PhysicsManager()
        {
            botRig.leftHand.rigTarget.localPosition = Vector3.Lerp(
                botRig.leftHand.rigTarget.localPosition,
                new Vector3(-0.35f, 0.12f, 0.15f),
                Time.deltaTime * 8f
            );
            botRig.rightHand.rigTarget.localPosition = Vector3.Lerp(
                botRig.rightHand.rigTarget.localPosition,
                new Vector3(0.35f, 0.12f, 0.15f),
                Time.deltaTime * 8f
            );

            if (isFalling)
            {
                RaycastHit hitInfo;
                Vector3 rayStart = botObject.transform.position + Vector3.up * 0.2f;

                if (Physics.Raycast(rayStart, Vector3.down, out hitInfo, 0.6f,
                    GorillaLocomotion.GTPlayer.Instance.locomotionEnabledLayers))
                {
                    isFalling = false;
                    isLimp = true;
                    recoverTimer = Time.time + recoverTime;
                    botRb.isKinematic = true;
                }
            }
            else if (isLimp && Time.time > recoverTimer)
            {
                isLimp = false;
                isReturning = true;
                botRb.isKinematic = true;
            }
        }

        void ParseFrameData(string frameData)
        {
            string[] parts = frameData.Split(',');
            if (parts.Length < 29) return;

            rootPos = new Vector3(float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
            rootRot = new Quaternion(float.Parse(parts[4]), float.Parse(parts[5]), float.Parse(parts[6]), float.Parse(parts[7]));

            headPos = new Vector3(float.Parse(parts[8]), float.Parse(parts[9]), float.Parse(parts[10]));
            headRot = new Quaternion(float.Parse(parts[11]), float.Parse(parts[12]), float.Parse(parts[13]), float.Parse(parts[14]));

            leftHandPos = new Vector3(float.Parse(parts[15]), float.Parse(parts[16]), float.Parse(parts[17]));
            leftHandRot = new Quaternion(float.Parse(parts[18]), float.Parse(parts[19]), float.Parse(parts[20]), float.Parse(parts[21]));

            rightHandPos = new Vector3(float.Parse(parts[22]), float.Parse(parts[23]), float.Parse(parts[24]));
            rightHandRot = new Quaternion(float.Parse(parts[25]), float.Parse(parts[26]), float.Parse(parts[27]), float.Parse(parts[28]));
        }

        void UpdateBotTransform()
        {
            float lerpSpeed = Time.deltaTime * 18f;

            if (isReturning)
            {
                Vector3 targetPos = new Vector3(rootPos.x, botObject.transform.position.y, rootPos.z);
                botObject.transform.position = Vector3.MoveTowards(
                    botObject.transform.position,
                    targetPos,
                    speed * Time.deltaTime
                );

                if (Vector3.Distance(botObject.transform.position, targetPos) < 0.2f)
                {
                    isReturning = false;
                }
            }
            else
            {
                botObject.transform.position = Vector3.Lerp(botObject.transform.position, rootPos, lerpSpeed);
                botObject.transform.rotation = Quaternion.Slerp(botObject.transform.rotation, rootRot, lerpSpeed);
            }

            botRig.head.rigTarget.localPosition = Vector3.Lerp(
                botRig.head.rigTarget.localPosition, headPos, lerpSpeed);
            botRig.head.rigTarget.localRotation = Quaternion.Slerp(
                botRig.head.rigTarget.localRotation, headRot, lerpSpeed);

            botRig.leftHand.rigTarget.localPosition = Vector3.Lerp(
                botRig.leftHand.rigTarget.localPosition, leftHandPos, lerpSpeed);
            botRig.leftHand.rigTarget.localRotation = Quaternion.Slerp(
                botRig.leftHand.rigTarget.localRotation, leftHandRot, lerpSpeed);

            botRig.rightHand.rigTarget.localPosition = Vector3.Lerp(
                botRig.rightHand.rigTarget.localPosition, rightHandPos, lerpSpeed);
            botRig.rightHand.rigTarget.localRotation = Quaternion.Slerp(
                botRig.rightHand.rigTarget.localRotation, rightHandRot, lerpSpeed);
        }

        void CreateBotInstance()
        {
            VRRig playerRig = GorillaTagger.Instance.offlineVRRig;
            botObject = Instantiate(playerRig.gameObject);
            botRig = botObject.GetComponent<VRRig>();
            botRig.enabled = false;

            botRb = botObject.GetComponent<Rigidbody>();
            if (botRb == null)
                botRb = botObject.AddComponent<Rigidbody>();

            botRb.isKinematic = true;
            botRb.useGravity = false;

            botObject.transform.localScale = Vector3.one * 0.5f;
        }
    }
}