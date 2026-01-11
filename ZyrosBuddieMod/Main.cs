using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using GorillaLocomotion;
using BepInEx;

namespace Main
{
    [BepInPlugin("Zyro.Buddie", "Zyro's Buddie", "0.0.1")]
    public class Main : BaseUnityPlugin
    {
        // not reccomened but you can change the variables below
        private List<string> recordedFrames = new List<string>();
        private bool animationActive = false;
        private float lastFrameTime = 0f;
        private int frameCounter = 0;

        private Vector3 goalRootPosition, goalHeadPosition, goalLeftPosition, goalRightPosition;
        private Quaternion goalRootRotation, goalHeadRotation, goalLeftRotation, goalRightRotation;

        private GameObject buddieCharacter;
        private VRRig buddieVRRig;
        private Rigidbody buddiePhysics;

        private bool grabbedByPlayer = false;
        private bool fallingDown = false;
        private bool ragdollMode = false;
        private bool navigatingBack = false;
        private float getUpTimer = 0f;

        private float navigationSpeed = 1.3f;
        private float floorOffset = 0.25f;
        private float wallCheckDistance = 1.2f;

        private void Start()
        {
            StartCoroutine(InitializeBuddie());
        }

        private IEnumerator InitializeBuddie()
        {
            while (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null)
            {
                yield return new WaitForSeconds(1f);
            }

            CreateBuddieCharacter();
            yield return StartCoroutine(FetchAnimationData());
        }

        // i will not include anything to help you add more animations
        private IEnumerator FetchAnimationData()
        {
            string dataSource1 = "https://raw.githubusercontent.com/zyroyz/GT-Buddie/refs/heads/main/BotData.txt";
            string dataSource2 = "https://raw.githubusercontent.com/zyroyz/GT-Buddie/refs/heads/main/BotData2.txt";

            string selectedUrl = (UnityEngine.Random.Range(0, 2) == 0) ? dataSource1 : dataSource2;

            using (UnityWebRequest request = UnityWebRequest.Get(selectedUrl))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string rawData = request.downloadHandler.text;
                    recordedFrames = new List<string>(rawData.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

                    frameCounter = 0;
                    animationActive = true;
                }
            }
        }

        private void LateUpdate()
        {
            if (GorillaTagger.Instance.offlineVRRig != null)
            {
                GorillaTagger.Instance.offlineVRRig.transform.localScale = Vector3.one;
            }

            if (buddieCharacter == null) return;

            bool leftHandGrabbing = ControllerInputPoller.instance.leftGrab;
            bool rightHandGrabbing = ControllerInputPoller.instance.rightGrab;

            float leftHandDistance = Vector3.Distance(GorillaTagger.Instance.leftHandTransform.position, buddieCharacter.transform.position);
            float rightHandDistance = Vector3.Distance(GorillaTagger.Instance.rightHandTransform.position, buddieCharacter.transform.position);

            if ((leftHandGrabbing && leftHandDistance < 0.5f) || (rightHandGrabbing && rightHandDistance < 0.5f))
            {
                grabbedByPlayer = true;
                fallingDown = false;
                ragdollMode = false;
                navigatingBack = false;

                buddiePhysics.isKinematic = true;
                buddiePhysics.useGravity = false;

                Transform activeHand = (leftHandGrabbing && leftHandDistance < 0.5f)
                    ? GorillaTagger.Instance.leftHandTransform
                    : GorillaTagger.Instance.rightHandTransform;

                buddieCharacter.transform.position = activeHand.position;
                buddieCharacter.transform.rotation = activeHand.rotation;
            }
            else if (grabbedByPlayer)
            {
                grabbedByPlayer = false;
                fallingDown = true;

                buddiePhysics.isKinematic = false;
                buddiePhysics.useGravity = true;
                buddiePhysics.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                buddiePhysics.linearVelocity = GorillaLocomotion.GTPlayer.Instance.GetComponent<Rigidbody>().linearVelocity;
                buddiePhysics.AddTorque(UnityEngine.Random.insideUnitSphere * 15f, ForceMode.Impulse);
            }

            if (grabbedByPlayer) return;

            if (fallingDown)
            {
                RaycastHit groundHit;
                LayerMask terrainMask = GorillaLocomotion.GTPlayer.Instance.locomotionEnabledLayers;

                if (Physics.SphereCast(buddieCharacter.transform.position + Vector3.up * 0.5f, 0.15f, Vector3.down, out groundHit, 1f, terrainMask))
                {
                    buddieCharacter.transform.position = groundHit.point + Vector3.up * floorOffset;
                    TransitionToRagdoll();
                }
            }

            if (fallingDown || ragdollMode)
            {
                buddieVRRig.leftHand.rigTarget.localPosition = Vector3.Lerp(
                    buddieVRRig.leftHand.rigTarget.localPosition,
                    new Vector3(-0.35f, 0.12f, 0.15f),
                    Time.deltaTime * 8f
                );

                buddieVRRig.rightHand.rigTarget.localPosition = Vector3.Lerp(
                    buddieVRRig.rightHand.rigTarget.localPosition,
                    new Vector3(0.35f, 0.12f, 0.15f),
                    Time.deltaTime * 8f
                );

                buddieVRRig.head.rigTarget.localPosition = Vector3.Lerp(
                    buddieVRRig.head.rigTarget.localPosition,
                    new Vector3(0, 0.15f, 0.25f),
                    Time.deltaTime * 8f
                );

                buddieVRRig.head.rigTarget.localRotation = Quaternion.Lerp(
                    buddieVRRig.head.rigTarget.localRotation,
                    Quaternion.Euler(80, 0, 0),
                    Time.deltaTime * 8f
                );

                if (ragdollMode && Time.time >= getUpTimer)
                {
                    ragdollMode = false;
                    navigatingBack = true;
                    buddiePhysics.isKinematic = true;
                    buddiePhysics.useGravity = false;
                }
                return;
            }

            if (animationActive && recordedFrames.Count > 0)
            {
                if (Time.time >= lastFrameTime)
                {
                    ParseFrameData(recordedFrames[frameCounter]);
                    frameCounter = (frameCounter + 1) % recordedFrames.Count;
                    lastFrameTime = Time.time + 0.0166f;
                }
                UpdateBuddieMovement();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (fallingDown) TransitionToRagdoll();
        }

        private void TransitionToRagdoll()
        {
            fallingDown = false;
            ragdollMode = true;
            getUpTimer = Time.time + 2f;
            buddiePhysics.angularVelocity = Vector3.zero;
            buddiePhysics.isKinematic = true;
        }

        private void ParseFrameData(string frameData)
        {
            string[] values = frameData.Split(',');
            if (values.Length < 29) return;

            int idx = 1;

            goalRootPosition = new Vector3(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalRootRotation = new Quaternion(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalHeadPosition = new Vector3(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalHeadRotation = new Quaternion(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalLeftPosition = new Vector3(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalLeftRotation = new Quaternion(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalRightPosition = new Vector3(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );

            goalRightRotation = new Quaternion(
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++]),
                float.Parse(values[idx++])
            );
        }

        private void UpdateBuddieMovement()
        {
            float lerpSpeed = Time.deltaTime * 18f;
            buddieCharacter.transform.localScale = Vector3.Lerp(
                buddieCharacter.transform.localScale,
                Vector3.one * 0.5f,
                lerpSpeed
            );

            if (navigatingBack)
            {
                Vector3 currentPosition2D = new Vector3(buddieCharacter.transform.position.x, 0, buddieCharacter.transform.position.z);
                Vector3 targetPosition2D = new Vector3(goalRootPosition.x, 0, goalRootPosition.z);
                float distanceToTarget = Vector3.Distance(currentPosition2D, targetPosition2D);

                if (distanceToTarget > 0.15f)
                {
                    Vector3 directionToTarget = (targetPosition2D - currentPosition2D).normalized;
                    Vector3 movementDirection = directionToTarget;
                    LayerMask environmentMask = GorillaLocomotion.GTPlayer.Instance.locomotionEnabledLayers;

                    if (Physics.Raycast(buddieCharacter.transform.position + Vector3.up * 0.5f, directionToTarget, wallCheckDistance, environmentMask))
                    {
                        Vector3 alternateLeft = Quaternion.Euler(0, -60, 0) * directionToTarget;
                        Vector3 alternateRight = Quaternion.Euler(0, 60, 0) * directionToTarget;

                        if (!Physics.Raycast(buddieCharacter.transform.position + Vector3.up * 0.5f, alternateLeft, wallCheckDistance, environmentMask))
                        {
                            movementDirection = alternateLeft;
                        }
                        else if (!Physics.Raycast(buddieCharacter.transform.position + Vector3.up * 0.5f, alternateRight, wallCheckDistance, environmentMask))
                        {
                            movementDirection = alternateRight;
                        }
                    }

                    Vector3 projectedPosition = buddieCharacter.transform.position + movementDirection * navigationSpeed * Time.deltaTime;

                    if (Physics.Raycast(projectedPosition + Vector3.up, Vector3.down, out RaycastHit terrainHit, 3f, environmentMask))
                    {
                        projectedPosition.y = terrainHit.point.y + floorOffset;
                    }

                    buddieCharacter.transform.position = projectedPosition;

                    if (movementDirection != Vector3.zero)
                    {
                        buddieCharacter.transform.rotation = Quaternion.Slerp(
                            buddieCharacter.transform.rotation,
                            Quaternion.LookRotation(movementDirection),
                            Time.deltaTime * 5f
                        );
                    }

                    float walkAnimSpeed = 10f;
                    float leftArmCycle = Time.time * walkAnimSpeed;
                    float rightArmCycle = (Time.time * walkAnimSpeed) + Mathf.PI;

                    float leftArmX = -0.3f;
                    float leftArmY = -0.15f + (Mathf.Sin(leftArmCycle) * 0.25f);
                    float leftArmZ = 0.2f + (Mathf.Cos(leftArmCycle) * 0.3f);

                    float rightArmX = 0.3f;
                    float rightArmY = -0.15f + (Mathf.Sin(rightArmCycle) * 0.25f);
                    float rightArmZ = 0.2f + (Mathf.Cos(rightArmCycle) * 0.3f);

                    buddieVRRig.leftHand.rigTarget.localPosition = Vector3.Lerp(
                        buddieVRRig.leftHand.rigTarget.localPosition,
                        new Vector3(leftArmX, leftArmY, leftArmZ),
                        Time.deltaTime * 15f
                    );

                    buddieVRRig.rightHand.rigTarget.localPosition = Vector3.Lerp(
                        buddieVRRig.rightHand.rigTarget.localPosition,
                        new Vector3(rightArmX, rightArmY, rightArmZ),
                        Time.deltaTime * 15f
                    );

                    float headBobAmount = Mathf.Abs(Mathf.Cos(Time.time * walkAnimSpeed)) * 0.08f;

                    buddieVRRig.head.rigTarget.localPosition = Vector3.Lerp(
                        buddieVRRig.head.rigTarget.localPosition,
                        new Vector3(0, 0.45f - headBobAmount, 0.1f),
                        Time.deltaTime * 10f
                    );

                    buddieVRRig.head.rigTarget.localRotation = Quaternion.Lerp(
                        buddieVRRig.head.rigTarget.localRotation,
                        Quaternion.identity,
                        Time.deltaTime * 10f
                    );
                }
                else
                {
                    navigatingBack = false;
                }
            }
            else
            {
                buddieCharacter.transform.position = Vector3.Lerp(
                    buddieCharacter.transform.position,
                    goalRootPosition,
                    lerpSpeed
                );

                buddieCharacter.transform.rotation = Quaternion.Slerp(
                    buddieCharacter.transform.rotation,
                    goalRootRotation,
                    lerpSpeed
                );

                buddieVRRig.head.rigTarget.localPosition = Vector3.Lerp(
                    buddieVRRig.head.rigTarget.localPosition,
                    goalHeadPosition,
                    lerpSpeed
                );

                buddieVRRig.head.rigTarget.localRotation = Quaternion.Slerp(
                    buddieVRRig.head.rigTarget.localRotation,
                    goalHeadRotation,
                    lerpSpeed
                );

                buddieVRRig.leftHand.rigTarget.localPosition = Vector3.Lerp(
                    buddieVRRig.leftHand.rigTarget.localPosition,
                    goalLeftPosition,
                    lerpSpeed
                );

                buddieVRRig.leftHand.rigTarget.localRotation = Quaternion.Slerp(
                    buddieVRRig.leftHand.rigTarget.localRotation,
                    goalLeftRotation,
                    lerpSpeed
                );

                buddieVRRig.rightHand.rigTarget.localPosition = Vector3.Lerp(
                    buddieVRRig.rightHand.rigTarget.localPosition,
                    goalRightPosition,
                    lerpSpeed
                );

                buddieVRRig.rightHand.rigTarget.localRotation = Quaternion.Slerp(
                    buddieVRRig.rightHand.rigTarget.localRotation,
                    goalRightRotation,
                    lerpSpeed
                );
            }
        }

        private void CreateBuddieCharacter()
        {
            VRRig playerRig = GorillaTagger.Instance.offlineVRRig;

            if (playerRig != null)
            {
                buddieCharacter = Instantiate(playerRig.gameObject);
                buddieVRRig = buddieCharacter.GetComponent<VRRig>();
                buddieVRRig.enabled = false;

                buddiePhysics = buddieCharacter.GetComponent<Rigidbody>();
                if (buddiePhysics == null)
                {
                    buddiePhysics = buddieCharacter.AddComponent<Rigidbody>();
                }

                buddiePhysics.isKinematic = true;
                buddiePhysics.useGravity = false;
                buddiePhysics.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                buddieCharacter.transform.position = new Vector3(-64.86f, 12.36f, -84.52f);
                buddieCharacter.transform.localScale = Vector3.one * 0.5f; // you can change this
            }
        }
    }
}