//#define IPAD

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

public class ARHATSelection : MonoBehaviour
{
    [SerializeField]
    ARPlaneManager _planeManager;

    [SerializeField]
    ARRaycastManager _raycastManager;

    [SerializeField]
    AROcclusionManager _occlusionManager;
    
    [SerializeField]
    ARCameraManager _cameraManager;

    [SerializeField]
    float _yRangeMin = 0.1439f;

    [SerializeField]
    float _yRangeMax = 0.82f;

    [SerializeField]
    bool _useEnhancedTouch = false;

    public void SetUseEnhancedTouch(bool useTouch)
    {
        _useEnhancedTouch = useTouch;
    }

    bool _testXMask = false;
    float _xRangeMin = 0f;
    float _xRangeMax = 1f;

    public void SetTestXMask(bool xMask, float xMin, float xMax)
    {
        _testXMask = xMask;
        _xRangeMin = xMin;
        _xRangeMax = xMax;
    }

    //[SerializeField]
    //ComputeShader _computeShader;

    //ComputeBuffer _depthBuffer;
    //ComputeBuffer _sphereTestResult;

    //int _raycastShader = -1;

    Matrix4x4 _flipYZ = Matrix4x4.identity;

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    //adding functionality to allow user to move spheres (or another object) within 3D space...
    [SerializeField]
    GameObject _markerPrefab;

    List<GameObject> _markerList = new List<GameObject>();
    float _minDist = 999999f;
    GameObject _currSelectedMarker;

    Camera _arCamera = null;
    Plane _currInfinitePlane;

    public void SetInfinitePlane(Plane p)
    {
        _currInfinitePlane = p;
    }

    // Start is called before the first frame update
    void Start()
    {
        _flipYZ.SetRow(2, new Vector4(0f,0f,-1f,0f));

        if(_useEnhancedTouch)
        {
            EnhancedTouchSupport.Enable();
        }

        _arCamera = null;
        _currInfinitePlane = new Plane(Vector3.zero, Vector3.zero);
        
        /*if(_computeShader != null)
        {
            _raycastShader = _computeShader.FindKernel("CSRayCastDepth");

            _depthBuffer = new ComputeBuffer(192*256, sizeof(int));

            _sphereTestResult = new ComputeBuffer(1, sizeof(int));

            int[] sphereDist = new int[1];
            sphereDist[0] = 100000;
            _sphereTestResult.SetData(sphereDist);

            _computeShader.SetBuffer("sphereMinDist", _sphereTestResult);
        } */
    }

    /*void OnDestroy()
    {
        if(_depthBuffer != null)
        {
            _depthBuffer.Release();
        }

        if(_sphereTestResult != null)
        {
            _sphereTestResult.Release();
        }
    }*/

    public void Reset()
    {
        /*if(_useEnhancedTouch)
        {
            EnhancedTouchSupport.Disable();
        }*/

        foreach(GameObject g in _markerList)
        {
            DestroyObject(g);
        }

        _markerList.Clear();
    }

    public bool RaycastWorldDepth(Vector3 rayStart, Vector3 rayDir, out Vector3 worldPoint, out float tDist)
    {
        worldPoint = Vector3.zero;
        tDist = 9999f;
        bool intersectedOnce = false;
        //testing first outside of compute shader...
        if(_occlusionManager != null)
        {
            Matrix4x4 viewMatrix =  Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one).inverse;

            if (SystemInfo.usesReversedZBuffer)
            {
                viewMatrix.m20 = -viewMatrix.m20;
                viewMatrix.m21 = -viewMatrix.m21;
                viewMatrix.m22 = -viewMatrix.m22;
                viewMatrix.m23 = -viewMatrix.m23;
            }

            Matrix4x4 viewInverse = viewMatrix.inverse * _flipYZ;

            Matrix4x4 _camIntrinsics = Matrix4x4.identity;
            
            if(!_cameraManager.subsystem.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                worldPoint = Vector4.zero;
                return false;
            }

            _camIntrinsics[0] = cameraIntrinsics.focalLength.y;
            _camIntrinsics[5] = cameraIntrinsics.focalLength.x;
            _camIntrinsics[8] = cameraIntrinsics.principalPoint.y;
            _camIntrinsics[9] = cameraIntrinsics.principalPoint.x;
            
            //_camIntrinsics.SetColumn(0, new Vector4(cameraIntrinsics.focalLength.y, 0f, 0f, 0f));
            //_camIntrinsics.SetColumn(1, new Vector4(0f, cameraIntrinsics.focalLength.x, 0f, 0f));
            //_camIntrinsics.SetColumn(2, new Vector4(cameraIntrinsics.principalPoint.y, cameraIntrinsics.principalPoint.x, 1f, 0f));

            Matrix4x4 camInv = _camIntrinsics.inverse;
            //environment depth image is RFloat format
            if(_occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage imageDepth))
            {
                byte[] depthData = imageDepth.GetPlane(0).data.ToArray();
                
                //256x192
                //touch position coords match screen orientation...
                //Debug.Log(touchPosition);
                float minDist = 9999f;

                for(int i = 0; i < imageDepth.dimensions.y; ++i)
                {
                    for(int j = 0; j < imageDepth.dimensions.x; ++j)
                    {
                        //float dX = i / Screen.width;
                        //float dY = j / Screen.height;

                        //dX = dX * 0.6163f + 0.1919f;

                        //dX = dX * 1536.0f;
                        //dY = dY * 2048.0f;

                        int depthX = (imageDepth.dimensions.y - i - 1);
                        int depthY = (imageDepth.dimensions.x - j - 1);

                        int dIdx = ((int)depthX * (int)imageDepth.dimensions.x * 4) + ((int)depthY * 4);

                        if(dIdx < depthData.Length)
                        {
                            float fDepth = System.BitConverter.ToSingle(depthData, dIdx);

                            if(fDepth > 0f)
                            {
                                float dXColor = (float)i * 7.5f;//touchPosition.x / Screen.width;
                                float dYColor = (float)j * 7.5f;//touchPosition.y / Screen.height; 
                                //dXColor = dXColor * 0.61627f + 0.1919f;
                                //dXColor = dXColor * 1440f;
                                //dYColor = dYColor * 1920f;
                                Vector3 touchVec = new Vector3((int)dXColor, (int)dYColor, 1f);
                                
                                Vector3 localPoint = camInv.MultiplyVector(touchVec);
                                localPoint *= fDepth;
                                Vector4 lp = new Vector4(localPoint.x, localPoint.y, localPoint.z, 1.0f);
                                Vector4 wp = viewInverse * lp;
                                //Debug.Log(viewInverse.ToString());
                                
                                if(wp.w != 0)
                                {
                                    wp.x = wp.x / wp.w;
                                    wp.y = wp.y / wp.w;
                                    wp.z = wp.z / wp.w;

                                    float sr = 0.02f;
                                    
                                    Vector3 wp3 = Vector3.zero;
                                    wp3.x = wp.x;
                                    wp3.y = wp.y;
                                    wp3.z = wp.z;

                                    float a = Vector3.Dot(rayDir, rayDir);
                                    Vector3 s0_r0 = rayStart - wp3;
                                    float b = 2.0f * Vector3.Dot(rayDir, s0_r0);
                                    float c = Vector3.Dot(s0_r0, s0_r0) - (sr * sr);
                                    if (b*b - 4.0f*a*c >= 0.0) 
                                    {
                                        float t = Mathf.Abs((-b - Mathf.Sqrt((b*b) - 4.0f*a*c))/(2.0f*a));
                                        if(t > 0.025 && t < minDist)
                                        {
                                            worldPoint.x = wp.x;
                                            worldPoint.y = wp.y;
                                            worldPoint.z = wp.z;
                                            tDist = t;
                                            minDist = t;
                                            intersectedOnce = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if(!intersectedOnce)
        {
            tDist = 0f;
        }
        
        return intersectedOnce;
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    bool RaycastEnhancedDepth(Touch touch, out Vector4 worldPoint)
    {
        if(_occlusionManager != null)
        {
            Matrix4x4 viewMatrix =  Matrix4x4.TRS(transform.position,transform.rotation, Vector3.one).inverse;

            if (SystemInfo.usesReversedZBuffer)
            {
                viewMatrix.m20 = -viewMatrix.m20;
                viewMatrix.m21 = -viewMatrix.m21;
                viewMatrix.m22 = -viewMatrix.m22;
                viewMatrix.m23 = -viewMatrix.m23;
            }

            Matrix4x4 viewInverse = viewMatrix.inverse * _flipYZ;

            Matrix4x4 _camIntrinsics = Matrix4x4.identity;
            
            if(!_cameraManager.subsystem.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                worldPoint = Vector4.zero;
                return false;
            }

            _camIntrinsics[0] = cameraIntrinsics.focalLength.y;
            _camIntrinsics[5] = cameraIntrinsics.focalLength.x;
            _camIntrinsics[8] = cameraIntrinsics.principalPoint.y;
            _camIntrinsics[9] = cameraIntrinsics.principalPoint.x;
            
            //_camIntrinsics.SetColumn(0, new Vector4(cameraIntrinsics.focalLength.y, 0f, 0f, 0f));
            //_camIntrinsics.SetColumn(1, new Vector4(0f, cameraIntrinsics.focalLength.x, 0f, 0f));
            //_camIntrinsics.SetColumn(2, new Vector4(cameraIntrinsics.principalPoint.y, cameraIntrinsics.principalPoint.x, 1f, 0f));

            Matrix4x4 camInv = _camIntrinsics.inverse;
            //environment depth image is RFloat format
            if(_occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage imageDepth))
            {
                byte[] depthData = imageDepth.GetPlane(0).data.ToArray();
                
                //256x192
                //touch position coords match screen orientation...
                //Debug.Log(touchPosition);

                float dX = touch.screenPosition.x / Screen.width;
                float dY = touch.screenPosition.y / Screen.height;
#if IPAD

#else
                dX = dX * 0.6163f + 0.1919f;
#endif

                dX = dX * 1536.0f;
                dY = dY * 2048.0f;

                int depthX = (imageDepth.dimensions.y - (int)(dX / 8f));
                int depthY = (imageDepth.dimensions.x - (int)(dY / 8f));

                int dIdx = ((int)depthX * (int)imageDepth.dimensions.x * 4) + ((int)depthY * 4);

                float fDepth = System.BitConverter.ToSingle(depthData, dIdx);

                if(fDepth > 0f)
                {
                    float dXColor = touch.screenPosition.x / Screen.width;
                    float dYColor = touch.screenPosition.y / Screen.height; 
                    dXColor = dXColor * 0.61627f + 0.1919f;
                    dXColor = dXColor * 1440f;
                    dYColor = dYColor * 1920f;
                    Vector3 touchVec = new Vector3((int)dXColor, (int)dYColor, 1f);
                    
                    Vector3 localPoint = camInv.MultiplyVector(touchVec);
                    localPoint *= fDepth;
                    Vector4 lp = new Vector4(localPoint.x, localPoint.y, localPoint.z, 1.0f);
                    worldPoint = viewInverse * lp;
                    //Debug.Log(viewInverse.ToString());
                    
                    if(worldPoint.w != 0)
                    {
                        worldPoint.x = worldPoint.x / worldPoint.w;
                        worldPoint.y = worldPoint.y / worldPoint.w;
                        worldPoint.z = worldPoint.z / worldPoint.w;
                        //worldPoint.w = worldPoint.w / worldPoint.w;
                        
                        //Debug.Log(worldPoint.ToString("F3"));
                        return true;
                    }
                }
            }
        }

        worldPoint = Vector4.zero;
        return false;
    }

    bool RaycastEnhancedDepthNormal(Touch touch, out Vector4 worldPoint, out Vector3 worldNormal)
    {
        worldNormal = Vector3.zero;
            
        if(_occlusionManager != null)
        {
            
            Matrix4x4 viewMatrix =  Matrix4x4.TRS(transform.position,transform.rotation, Vector3.one).inverse;

            if (SystemInfo.usesReversedZBuffer)
            {
                viewMatrix.m20 = -viewMatrix.m20;
                viewMatrix.m21 = -viewMatrix.m21;
                viewMatrix.m22 = -viewMatrix.m22;
                viewMatrix.m23 = -viewMatrix.m23;
            }

            Matrix4x4 viewInverse = viewMatrix.inverse * _flipYZ;

            Matrix4x4 _camIntrinsics = Matrix4x4.identity;
            
            if(!_cameraManager.subsystem.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
            {
                worldPoint = Vector4.zero;
                return false;
            }

            _camIntrinsics[0] = cameraIntrinsics.focalLength.y;
            _camIntrinsics[5] = cameraIntrinsics.focalLength.x;
            _camIntrinsics[8] = cameraIntrinsics.principalPoint.y;
            _camIntrinsics[9] = cameraIntrinsics.principalPoint.x;
            
            //_camIntrinsics.SetColumn(0, new Vector4(cameraIntrinsics.focalLength.y, 0f, 0f, 0f));
            //_camIntrinsics.SetColumn(1, new Vector4(0f, cameraIntrinsics.focalLength.x, 0f, 0f));
            //_camIntrinsics.SetColumn(2, new Vector4(cameraIntrinsics.principalPoint.y, cameraIntrinsics.principalPoint.x, 1f, 0f));

            Matrix4x4 camInv = _camIntrinsics.inverse;
            //environment depth image is RFloat format
            if(_occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage imageDepth))
            {
                byte[] depthData = imageDepth.GetPlane(0).data.ToArray();
                
                //256x192
                //touch position coords match screen orientation...
                //Debug.Log(touchPosition);

                float dX = touch.screenPosition.x / Screen.width;
                float dY = touch.screenPosition.y / Screen.height;

                float neighborUp = touch.screenPosition.y + 32f;
                float neighborOver = touch.screenPosition.x + 32f;
                //don't have to worry about bounds check here as we don't accept touches within outside of a y range anyways..
                float dYUp = neighborUp / Screen.height;
                float dXUp = neighborOver / Screen.width;

#if IPAD

#else
                dX = dX * 0.6163f + 0.1919f;
#endif

                dX = dX * 1536.0f;
                dY = dY * 2048.0f;
#if IPAD

#else
                dXUp = dXUp * 0.6163f + 0.1919f;
#endif

                dXUp = dXUp * 1536.0f;
                dYUp = dYUp * 2048.0f;

                int depthX = (imageDepth.dimensions.y - (int)(dX / 8f));
                int depthY = (imageDepth.dimensions.x - (int)(dY / 8f));

                int depthXOver = (imageDepth.dimensions.y - (int)(dXUp / 8f));
                int depthYUp = (imageDepth.dimensions.x - (int)(dYUp / 8f));

                int dIdx = ((int)depthX * (int)imageDepth.dimensions.x * 4) + ((int)depthY * 4);
                int dIdxUp = ((int)depthXOver * (int)imageDepth.dimensions.x * 4) + ((int)depthYUp * 4);

                float fDepth = System.BitConverter.ToSingle(depthData, dIdx);

                if(fDepth > 0f)
                {
                    float fDepthUp = System.BitConverter.ToSingle(depthData, dIdxUp);

                    float dXColor = touch.screenPosition.x / Screen.width;
                    float dYColor = touch.screenPosition.y / Screen.height; 
#if IPAD
#else
                    dXColor = dXColor * 0.61627f + 0.1919f;
#endif
                    dXColor = dXColor * 1440f;
                    dYColor = dYColor * 1920f;
                    Vector3 touchVec = new Vector3((int)dXColor, (int)dYColor, 1f);
                    
                    Vector3 localPoint = camInv.MultiplyVector(touchVec);
                    localPoint *= fDepth;
                    Vector4 lp = new Vector4(localPoint.x, localPoint.y, localPoint.z, 1.0f);
                    worldPoint = viewInverse * lp;
                    //Debug.Log(viewInverse.ToString());
                    
                    if(worldPoint.w != 0)
                    {
                        worldPoint.x = worldPoint.x / worldPoint.w;
                        worldPoint.y = worldPoint.y / worldPoint.w;
                        worldPoint.z = worldPoint.z / worldPoint.w;
                        worldPoint.w = 1f;
                        //worldPoint.w = worldPoint.w / worldPoint.w;
                        
                        float dXColorUp = (touch.screenPosition.x + 32f) / Screen.width;
                        float dYColorUp = (touch.screenPosition.y + 32f) / Screen.height; 
#if IPAD
#else
                        dXColorUp = dXColorUp * 0.61627f + 0.1919f;
#endif
                        dXColorUp = dXColorUp * 1440f;
                        dYColorUp = dYColorUp * 1920f;
                        Vector3 touchVecUp = new Vector3((int)dXColorUp, (int)dYColorUp, 1f);
                        
                        Vector3 localPointUp = camInv.MultiplyVector(touchVecUp);
                        localPointUp *= fDepthUp;
                        Vector4 lpUp = new Vector4(localPointUp.x, localPointUp.y, localPointUp.z, 1.0f);
                        Vector4 worldPointUp = viewInverse * lpUp;
                        //Debug.Log(viewInverse.ToString());
                        
                        if(worldPointUp.w != 0)
                        {
                            worldPointUp.x = worldPointUp.x / worldPointUp.w;
                            worldPointUp.y = worldPointUp.y / worldPointUp.w;
                            worldPointUp.z = worldPointUp.z / worldPointUp.w;
                            worldPointUp.w = 1f;

                            Vector3 vToUp = new Vector3(worldPointUp.x - worldPoint.x, worldPointUp.y - worldPoint.y, worldPointUp.z - worldPoint.z);
                            
                            vToUp = Vector3.Normalize(vToUp);
                            //Debug.Log(vToUp.ToString("F2"));

                            if(_arCamera == null)
                            {
                                _arCamera = Camera.main;
                            }

                            Vector3 n = Vector3.Cross(vToUp, Vector3.up);
                            
                            Vector3 wpu = Vector3.zero;
                            wpu.x = worldPoint.x;
                            wpu.y = worldPoint.y;
                            wpu.z = worldPoint.z;

                           
                            Vector3 toCamera = Vector3.Normalize(_arCamera.gameObject.transform.position - wpu);

                            if(Vector3.Dot(toCamera, n) < 0f)
                            {
                                n = -n;
                            }

                            worldNormal = n;//Vector3.Cross(n, Vector3.up);
                            //Debug.Log("World normal: " + worldNormal.ToString("F2"));
                            // _markerList.Add(Instantiate(_markerPrefab, worldPointUp, Quaternion.LookRotation(worldNormal, Vector3.up)));

                            //Debug.Log(worldPoint.ToString("F3"));
                            return true;
                        }
                    }
                }
            }
        }

        worldPoint = Vector4.zero;
        return false;
    }

    public GameObject ManualMarkerAdd(Vector3 pos, Quaternion rot)
    {
        _markerList.Add(Instantiate(_markerPrefab, pos, rot));
        return _markerList[_markerList.Count-1];
    }

    public bool TryTouchMoveMarkerNormal(int markerLimit, out GameObject marker)
    {
        /*if(EventSystem.current.IsPointerOverGameObject())
        {
            marker = null;
            return false;
        }*/

        Vector2 touchPos = Vector2.zero;
        if(TryGetTouch(out touchPos))
        {
            Touch touch = Touch.activeTouches[0];
            _minDist = float.MaxValue;
            
            if (touch.phase == TouchPhase.Began)
            {
                if(RaycastEnhancedDepthNormal(touch, out Vector4 worldPoint, out Vector3 worldNormal))
                {
                    int closestIndex = -1;
            
                    Vector3 hitPos = Vector3.zero;
                    hitPos.x = worldPoint.x;
                    hitPos.y = worldPoint.y;
                    hitPos.z = worldPoint.z;

                    //Debug.Log(lookDir.ToString("F3"));

                    Quaternion rot = Quaternion.LookRotation(worldNormal, Vector3.up);
                    //find closest point. If closer than 0.05f save. If none, spawn new dot and save, also do line rendering.
                    for (int i = 0; i < _markerList.Count; i++)
                    {
                        float fDist = Vector3.Distance(_markerList[i].transform.position, hitPos);
                        if (fDist < _minDist)
                        {
                            _currSelectedMarker = _markerList[i];
                            _minDist = fDist;
                            closestIndex = i;
                            
                        }
                    }//found point closest to hitPos. closestPoint for obj, minDist for distance, closestIndex for its loc in PlacedDots

                    if (_minDist > 0.05f)
                    {   
                        //there is no dot in range (5 cm), spawn one and save it
                        if(_markerList.Count < markerLimit)
                        {
                            //Debug.Log("Instantiating marker");
                            _markerList.Add(Instantiate(_markerPrefab, hitPos, rot));
                            _currSelectedMarker = _markerList[_markerList.Count - 1];//set last dot in list as dragged dot
                            //lineRenderer.positionCount = _markerList.Count; //so it knows how many lines to draw
                            closestIndex = _markerList.Count - 1;
                            marker = _markerList[closestIndex];
                        }
                        else
                        {
                            if(closestIndex > -1) {
                                _currSelectedMarker = _markerList[closestIndex];
                            }
                        }

                        //lineRenderer.SetPosition(closestIndex, PlacedDots[closestIndex].transform.position);
                        //places the i'th position marker (closestIndex) (internally saved in line renderer) to be the same as PlacedDots[i].transform.position

                        //DistanceTextPlacer(closestIndex, true);
                    }//else: there is a dot in range. It's saved in closestPoint
                    else
                    {
                        _currSelectedMarker = _markerList[closestIndex];
                    }
                }
                else
                {
                    _currSelectedMarker = null;
                }

            }
            else if (touch.phase == TouchPhase.Moved)
            {
                //return if no saved point. Else move the saved point. Update the lines.
                if (_currSelectedMarker == null) 
                {
                    marker = null;
                    return false;
                }

                if(RaycastEnhancedDepthNormal(touch, out Vector4 worldPoint, out Vector3 worldNormal))
                {
                    Vector3 hitPos = Vector3.zero;
                    hitPos.x = worldPoint.x;
                    hitPos.y = worldPoint.y;
                    hitPos.z = worldPoint.z;

                    //Debug.Log("**:" + worldNormal.ToString("F3"));
                    
                    _currSelectedMarker.transform.position = hitPos;
                    _currSelectedMarker.transform.rotation = Quaternion.LookRotation(worldNormal, Vector3.up);
                }
                
                //lineRenderer.SetPosition(closestIndex, PlacedDots[closestIndex].transform.position);
                //places the i'th position marker (closestIndex) (internally saved in line renderer) to be the same as PlacedDots[i].transform.position

                //DistanceTextPlacer(closestIndex, false);
            }
            else if(touch.phase == TouchPhase.Ended)
            {
                //clean the saved point
                _currSelectedMarker = null;
            }

            marker = _currSelectedMarker;
            return true;
        }

        marker = null;
        return false;
    }

    public bool TryTouchMoveMarkerRange(int markerBegin, int markerEnd, out GameObject marker)
    {
        /*if(EventSystem.current.IsPointerOverGameObject())
        {
            marker = null;
            return false;
        }*/

        Vector2 touchPos = Vector2.zero;
        if(TryGetTouch(out touchPos))
        {
            Touch touch = Touch.activeTouches[0];
            _minDist = float.MaxValue;
            
            if (touch.phase == TouchPhase.Began)
            {
                if(RaycastEnhancedDepth(touch, out Vector4 worldPoint))
                {
                    int closestIndex = -1;
            
                    Vector3 hitPos = Vector3.zero;
                    hitPos.x = worldPoint.x;
                    hitPos.y = worldPoint.y;
                    hitPos.z = worldPoint.z;

                    //find closest point. If closer than 0.05f save. If none, spawn new dot and save, also do line rendering.
                    for (int i = markerBegin; i < markerEnd; i++)
                    {
                        float fDist = Vector3.Distance(_markerList[i].transform.position, hitPos);
                        if (fDist < _minDist)
                        {
                            _currSelectedMarker = _markerList[i];
                            _minDist = fDist;
                            closestIndex = i;
                            
                        }
                    }//found point closest to hitPos. closestPoint for obj, minDist for distance, closestIndex for its loc in PlacedDots
                    _currSelectedMarker = _markerList[closestIndex];
                    
                }
                else
                {
                    _currSelectedMarker = null;
                }

            }
            else if (touch.phase == TouchPhase.Moved)
            {
                //return if no saved point. Else move the saved point. Update the lines.
                if (_currSelectedMarker == null) 
                {
                    marker = null;
                    return false;
                }

                if(RaycastEnhancedDepth(touch, out Vector4 worldPoint))
                {
                    Vector3 hitPos = Vector3.zero;
                    hitPos.x = worldPoint.x;
                    hitPos.y = worldPoint.y;
                    hitPos.z = worldPoint.z;

                    _currSelectedMarker.transform.position = hitPos;
                }
                
                //lineRenderer.SetPosition(closestIndex, PlacedDots[closestIndex].transform.position);
                //places the i'th position marker (closestIndex) (internally saved in line renderer) to be the same as PlacedDots[i].transform.position

                //DistanceTextPlacer(closestIndex, false);
            }
            else if(touch.phase == TouchPhase.Ended)
            {
                //clean the saved point
                _currSelectedMarker = null;
            }

            marker = _currSelectedMarker;
            return true;
        }

        marker = null;
        return false;
    }


    public bool TryTouchMoveMarkerRangePlane(int markerBegin, int markerEnd, out GameObject marker)
    {
        /*if(EventSystem.current.IsPointerOverGameObject())
        {
            marker = null;
            return false;
        }*/

        Vector2 touchPos = Vector2.zero;
        if(TryGetTouch(out touchPos))
        {
            Touch touch = Touch.activeTouches[0];
            //_minDist = float.MaxValue;
            
            TrackableId planeID = TrackableId.invalidId;

            if (touch.phase == TouchPhase.Began)
            {
                //if starting a new selection, we've either previously selected a plane or haven't
                if(_arCamera == null)
                {
                    _arCamera = Camera.main;
                }

                if(_currInfinitePlane.normal.magnitude == 0)
                {
                    //could instead raycast againt depth map...
                    //if(RaycastEnhancedDepthNormal(touch, out Vector4 wp, out Vector3 wn))
                    if(RaycastPlaneInfinity(touchPos, out planeID))
                    {
                        //currently forcing to be up...
                        //_currInfinitePlane = new Plane(new Vector3(wp.x, wp.y, wp.z), Vector3.up);
                        _currInfinitePlane = _planeManager.GetPlane(planeID).infinitePlane;
                    }
                    else
                    {
                        _currSelectedMarker = null;
                        marker = null;
                        return false;
                    }

                }
                
                Ray ray = _arCamera.ScreenPointToRay(touchPos);

                float enter = 0.0f;

                if (_currInfinitePlane.Raycast(ray, out enter))
                {
                    //Get the point that is clicked
                    Vector3 hitPoint = ray.GetPoint(enter);

                    int closestIndex = -1;

                    float minDist = 0.1f;
                    Vector3 hitPos = hitPoint;
                    //find closest point. If closer than 0.05f save. If none, spawn new dot and save, also do line rendering.
                    for (int i = markerBegin; i < markerEnd; i++)
                    {
                        float fDist = Vector3.Distance(_markerList[i].transform.position, hitPos);
                        if (fDist < minDist)
                        {
                            _currSelectedMarker = _markerList[i];
                            minDist = fDist;
                            closestIndex = i;
                            
                        }
                    }//found point closest to hitPos. closestPoint for obj, minDist for distance, closestIndex for its loc in PlacedDots

                    if(closestIndex != -1)
                    {
                        _currSelectedMarker = _markerList[closestIndex];
                    }
                    else
                    {
                        //if didn't select close enough to one of the markers, try to select a new plane...
                        if(RaycastPlaneInfinity(touchPos, out planeID))
                        //if(RaycastEnhancedDepthNormal(touch, out Vector4 wp2, out Vector3 wn2))
                        {
                            //_currInfinitePlane = new Plane(new Vector3(wp2.x, wp2.y, wp2.z), Vector3.up);
                            _currInfinitePlane = _planeManager.GetPlane(planeID).infinitePlane;
                        }
                        else
                        {
                            _currSelectedMarker = null;
                            marker = null;
                            return false;
                        }
                    }
                }

            }
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
            {
                //return if no saved point. Else move the saved point. Update the lines.
                if (_currSelectedMarker == null) 
                {
                    marker = null;
                    return false;
                }

                if(_arCamera == null)
                {
                    _arCamera = Camera.main;
                }

                Ray ray = _arCamera.ScreenPointToRay(touchPos);

                //Initialise the enter variable
                float enter = 0.0f;

                if (_currInfinitePlane.Raycast(ray, out enter))
                {
                    //Get the point that is clicked
                    Vector3 hitPoint = ray.GetPoint(enter);
                    //if(RaycastPlaneInfinity(touchPos, out planeID))
                    {
                        //_currSelectedMarker.transform.position = s_Hits[0].pose.position;
                        _currSelectedMarker.transform.position = hitPoint;
                    }
                }
                //lineRenderer.SetPosition(closestIndex, PlacedDots[closestIndex].transform.position);
                //places the i'th position marker (closestIndex) (internally saved in line renderer) to be the same as PlacedDots[i].transform.position

                //DistanceTextPlacer(closestIndex, false);
            }
            else if(touch.phase == TouchPhase.Ended)
            {
                //clean the saved point
                _currSelectedMarker = null;
            }

            marker = _currSelectedMarker;
            return true;
        }

        marker = null;
        return false;
    }

    public bool TryTouchMoveMarker(int markerLimit, out GameObject marker)
    {
        /*if(EventSystem.current.IsPointerOverGameObject())
        {
            marker = null;
            return false;
        }*/

        Vector2 touchPos = Vector2.zero;
        if(TryGetTouch(out touchPos))
        {
            Touch touch = Touch.activeTouches[0];
            _minDist = float.MaxValue;
            
            if (touch.phase == TouchPhase.Began)
            {
                if(RaycastEnhancedDepth(touch, out Vector4 worldPoint))
                {
                    int closestIndex = -1;
            
                    Vector3 hitPos = Vector3.zero;
                    hitPos.x = worldPoint.x;
                    hitPos.y = worldPoint.y;
                    hitPos.z = worldPoint.z;

                    //find closest point. If closer than 0.05f save. If none, spawn new dot and save, also do line rendering.
                    for (int i = 0; i < _markerList.Count; i++)
                    {
                        float fDist = Vector3.Distance(_markerList[i].transform.position, hitPos);
                        if (fDist < _minDist)
                        {
                            _currSelectedMarker = _markerList[i];
                            _minDist = fDist;
                            closestIndex = i;
                            
                        }
                    }//found point closest to hitPos. closestPoint for obj, minDist for distance, closestIndex for its loc in PlacedDots

                    if (_minDist > 0.05f)
                    {   
                        //there is no dot in range (5 cm), spawn one and save it
                        if(_markerList.Count < markerLimit)
                        {
                            //Debug.Log("Instantiating marker");
                            _markerList.Add(Instantiate(_markerPrefab, hitPos, Quaternion.identity));
                            _currSelectedMarker = _markerList[_markerList.Count - 1];//set last dot in list as dragged dot
                            //lineRenderer.positionCount = _markerList.Count; //so it knows how many lines to draw
                            closestIndex = _markerList.Count - 1;
                            marker = _markerList[closestIndex];
                        }

                        //lineRenderer.SetPosition(closestIndex, PlacedDots[closestIndex].transform.position);
                        //places the i'th position marker (closestIndex) (internally saved in line renderer) to be the same as PlacedDots[i].transform.position

                        //DistanceTextPlacer(closestIndex, true);
                    }//else: there is a dot in range. It's saved in closestPoint
                    else
                    {
                        _currSelectedMarker = _markerList[closestIndex];
                        marker = _markerList[closestIndex];
                    }
                }
                else
                {
                    _currSelectedMarker = null;
                }

            }
            else if (touch.phase == TouchPhase.Moved)
            {
                //return if no saved point. Else move the saved point. Update the lines.
                if (_currSelectedMarker == null) 
                {
                    marker = null;
                    return false;
                }

                if(RaycastEnhancedDepth(touch, out Vector4 worldPoint))
                {
                    Vector3 hitPos = Vector3.zero;
                    hitPos.x = worldPoint.x;
                    hitPos.y = worldPoint.y;
                    hitPos.z = worldPoint.z;

                    _currSelectedMarker.transform.position = hitPos;
                }
                
                //lineRenderer.SetPosition(closestIndex, PlacedDots[closestIndex].transform.position);
                //places the i'th position marker (closestIndex) (internally saved in line renderer) to be the same as PlacedDots[i].transform.position

                //DistanceTextPlacer(closestIndex, false);
            }
            else if(touch.phase == TouchPhase.Ended)
            {
                //clean the saved point
                _currSelectedMarker = null;
            }

            marker = _currSelectedMarker;
            return true;
        }

        marker = null;
        return false;
    }

    bool TryGetTouch(out Vector2 touchPosition)
    {
        touchPosition = Vector2.zero;

        if(_useEnhancedTouch)
        {
            return TryGetEnhancedTouchPosition(out touchPosition);
        }
        else
        {
            return TryGetTouchPosition(out touchPosition);
        }
    }

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
        //if (Touch.activeTouches.Count > 0)
        if(Input.touchCount > 0)
        {
            //touchPosition = Touch.activeTouches[0].screenPosition;
            touchPosition = Input.GetTouch(0).position;

            float yFrac = ((float)touchPosition.y) / (float)Screen.height;

            if(yFrac > _yRangeMin && yFrac < _yRangeMax)
            {
                //Debug.Log(touchPosition.ToString("F3"));
                return true;
            }
        }

        touchPosition = default;
        //Debug.Log("Default: " + touchPosition.ToString("F3"));

        return false;
    }

    bool TryGetEnhancedTouchPosition(out Vector2 touchPosition)
    {
        if (Touch.activeTouches.Count > 0)
        {
            touchPosition = Touch.activeTouches[0].screenPosition;
            float yFrac = ((float)touchPosition.y) / (float)Screen.height;

            if(yFrac > _yRangeMin && yFrac < _yRangeMax)
            {
                if(_testXMask)
                {
                    //Debug.Log(touchPosition.x);
                    float xFrac = ((float)touchPosition.x) / (float)Screen.width;
                    //Debug.Log(xFrac);
                    if(xFrac > _xRangeMin && xFrac < _xRangeMax)
                    {
                        return true;
                    }
                }
                else
                {
                    //Debug.Log(touchPosition.ToString("F3"));
                    return true;
                }
            }


            //return true;
        }

        touchPosition = default;
        return false;
    }

    public bool TryGetEnhanced(out Vector2 touchPosition)
    {
        return TryGetEnhancedTouchPosition(out touchPosition);
    }
    
    public bool RaycastDepthAndPlane(out Vector4 worldPoint, out TrackableId planeID)
    {
        TrackableId pID = TrackableId.invalidId;
        bool planeHit = RaycastPlane(out pID);
        Vector4 wp = Vector4.zero;
        bool depthHit = RaycastDepth(out wp);

        if(depthHit && planeHit)
        {
            worldPoint = wp;
            planeID = pID;
            return true;
        }

        worldPoint = Vector4.zero;
        planeID = TrackableId.invalidId;
        return false;
    }

    
    public bool RaycastDepth(out Vector4 worldPoint)
    {
        if(TryGetTouch(out Vector2 touchPosition))
        {
            if(_occlusionManager != null)
            {
                Matrix4x4 viewMatrix =  Matrix4x4.TRS(transform.position,transform.rotation, Vector3.one).inverse;

                if (SystemInfo.usesReversedZBuffer)
                {
                    viewMatrix.m20 = -viewMatrix.m20;
                    viewMatrix.m21 = -viewMatrix.m21;
                    viewMatrix.m22 = -viewMatrix.m22;
                    viewMatrix.m23 = -viewMatrix.m23;
                }

                Matrix4x4 viewInverse = viewMatrix.inverse * _flipYZ;

                Matrix4x4 _camIntrinsics = Matrix4x4.identity;
                
                if(!_cameraManager.subsystem.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
                {
                    worldPoint = Vector4.zero;
                    return false;
                }

                _camIntrinsics[0] = cameraIntrinsics.focalLength.y;
                _camIntrinsics[5] = cameraIntrinsics.focalLength.x;
                _camIntrinsics[8] = cameraIntrinsics.principalPoint.y;
                _camIntrinsics[9] = cameraIntrinsics.principalPoint.x;
                
                //_camIntrinsics.SetColumn(0, new Vector4(cameraIntrinsics.focalLength.y, 0f, 0f, 0f));
                //_camIntrinsics.SetColumn(1, new Vector4(0f, cameraIntrinsics.focalLength.x, 0f, 0f));
                //_camIntrinsics.SetColumn(2, new Vector4(cameraIntrinsics.principalPoint.y, cameraIntrinsics.principalPoint.x, 1f, 0f));

                Matrix4x4 camInv = _camIntrinsics.inverse;
                //environment depth image is RFloat format
                if(_occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage imageDepth))
                {
                    byte[] depthData = imageDepth.GetPlane(0).data.ToArray();
                    
                    //256x192
                    //touch position coords match screen orientation...
                    //Debug.Log(touchPosition);

                    float dX = touchPosition.x / Screen.width;
                    float dY = touchPosition.y / Screen.height;

#if IPAD

#else
                    dX = dX * 0.6163f + 0.1919f;
#endif

                    dX = dX * 1536.0f;
                    dY = dY * 2048.0f;

                    int depthX = (imageDepth.dimensions.y - (int)(dX / 8f));
                    int depthY = (imageDepth.dimensions.x - (int)(dY / 8f));

                    int dIdx = ((int)depthX * (int)imageDepth.dimensions.x * 4) + ((int)depthY * 4);

                    float fDepth = System.BitConverter.ToSingle(depthData, dIdx);

                    if(fDepth > 0f)
                    {
                        float dXColor = touchPosition.x / Screen.width;
                        float dYColor = touchPosition.y / Screen.height; 
                        dXColor = dXColor * 0.61627f + 0.1919f;
                        dXColor = dXColor * 1440f;
                        dYColor = dYColor * 1920f;
                        Vector3 touchVec = new Vector3((int)dXColor, (int)dYColor, 1f);
                        
                        Vector3 localPoint = camInv.MultiplyVector(touchVec);
                        localPoint *= fDepth;
                        Vector4 lp = new Vector4(localPoint.x, localPoint.y, localPoint.z, 1.0f);
                        worldPoint = viewInverse * lp;
                        //Debug.Log(viewInverse.ToString());
                        
                        if(worldPoint.w != 0)
                        {
                            worldPoint.x = worldPoint.x / worldPoint.w;
                            worldPoint.y = worldPoint.y / worldPoint.w;
                            worldPoint.z = worldPoint.z / worldPoint.w;
                            //worldPoint.w = worldPoint.w / worldPoint.w;
                            
                            //Debug.Log(worldPoint.ToString("F3"));
                            return true;
                        }
                    }
                }
            }
        }

        worldPoint = Vector4.zero;
        return false;

        /*if(_raycastManager != null && _planeManager != null)
        {
            Debug.Log("Trying raycast");
            if (_raycastManager.Raycast(touchPosition, s_Hits, TrackableType.Depth))
            {
                Debug.Log("Hit: " + s_Hits[0].distance);
                _sphereTest.transform.position = s_Hits[0].pose.position;
                _sphereTest.transform.up = s_Hits[0].pose.up;
            }
        }*/
    }

    public bool RaycastPlane(out TrackableId planeID)
    {
        if (TryGetTouch(out Vector2 touchPosition))
        {
            if(_raycastManager != null && _planeManager != null)
            {
                if (_raycastManager.Raycast(touchPosition, s_Hits, TrackableType.PlaneWithinPolygon))
                {
                    planeID = s_Hits[0].trackableId;
                    return true;
                }
            }
        }

        planeID = TrackableId.invalidId;
        return false;
    }

    public bool RaycastPlaneInfinity(Vector2 touchPosition, out TrackableId planeID)
    {
        if(_raycastManager != null && _planeManager != null)
        {
            if (_raycastManager.Raycast(touchPosition, s_Hits, TrackableType.PlaneWithinBounds))//PlaneWithinInfinity))
            {
                
                planeID = s_Hits[0].trackableId;
                //Debug.Log("Num hits: " + s_Hits.Count);
                //Debug.Log("Plane ID: " + planeID);
                return true;
            }
        }

        planeID = TrackableId.invalidId;
        return false;
    }
}
