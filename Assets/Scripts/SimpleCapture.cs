#define STORE_NORMALS
#define WRITE_FRAMES
#define DIRECT_DEPTH_WRITE
#define USE_CS
#define IPAD

using System;
using System.Text;

using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.IO;
#if UNITY_IOS
using System.Runtime.InteropServices;
using UnityEngine.XR.ARKit;
#endif
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using OpenCVForUnity.CoreModule;
//using OpenCVForUnity.ImgprocModule;
//using OpenCVForUnity.UnityUtils;
//using OpenCVForUnity.UnityUtils.Helper;
//using OpenCVForUnity.Features2dModule;

//namespace UnityEngine.XR.ARFoundation.Samples
//{
    /// <summary>
    /// This component displays a picture-in-picture view of the environment depth texture, the human depth texture, or
    /// the human stencil texture.
    /// </summary>

    public class SimpleCapture : MonoBehaviour
    {
        /// <summary>
        /// A string builder for construction of strings.
        /// </summary>
        readonly StringBuilder m_StringBuilder = new StringBuilder();

        /// <summary>
        /// The current screen orientation remembered so that we are only updating the raw image layout when it changes.
        /// </summary>
        ScreenOrientation m_CurrentScreenOrientation;

        /// <summary>
        /// The display rotation matrix for the shader.
        /// </summary.
        Matrix4x4 m_DisplayRotationMatrix = Matrix4x4.identity;

#if UNITY_ANDROID
        /// <summary>
        /// A matrix to flip the Y coordinate for the Android platform.
        /// </summary>
        Matrix4x4 k_AndroidFlipYMatrix = Matrix4x4.identity;
#endif // UNITY_ANDROID

		bool _bIsCapturing = false;

		[SerializeField]
		bool _captureOnce = false;

		[SerializeField]
		bool _startWithQRCode = false;

		bool _bDoneCapturing = false;
		bool _bCapturedColor = false;
		bool _bCapturedDepth = false;
		
		List<GameObject> _detectedDents = new List<GameObject>();
		[SerializeField]
		ARAnchorManager _anchorManager;

		[SerializeField]
		QRCodeManager _qrManager;

		[SerializeField]
		GameObject _anchorPrefab;

		public enum AppMode
		{
			eHAIL
		}

		AppMode _currentMode = AppMode.eHAIL;

		float _lastExposure = 1.0f;
		float _thisExposure = 1.0f;

		float _lastIntensity = 1000.0f;

		float _thisIntensity = 1000.0f;

		float _lastColorTemp = 6500.0f;
		float _thisColorTemp = 6500.0f;

		float _lastCameraGrain = 1.0f;

		float _threshold = 224f;
		float _thresholdPos = 224f;
		float _thresholdNeg = 224f;

		int _numBlurs = 2;

		const float _moveThreshold = 0.02f;

		const int FLOW_WINDOW_SIZE = 5;
		
		//Mat[] _flowColor;
		//Mat _flowTempColor;

		bool _doOpticalFlow = false;
		int _opticalFlowCount = 0;
		int _opticalFlowWriteCount = 0;

		int _pressCount = 0;

		bool _firstFrame = true;

		public RenderTexture _renderTargetColorV;
		public RenderTexture _renderTargetDepthV;
		public RenderTexture _renderTargetDepthVSmall;
		public RenderTexture _renderTargetConfV;
		public RenderTexture _renderTargetConfVUnrotated;

		RenderTexture _normalsRenderTexture;
		RenderTexture _geometryRenderTexture;
		RenderTexture _laplacianTexture;
	
		public Material _confUnrotatedCopyMaterial;
		public Material _colorCopyMaterial;
		public Material _colorCopyBasicMaterial;

		//[SerializeField]
		float _kernelHalf = 4f;
		float _depthMult = 1f;
		float _maxCircleRadius = 600.0f;
		float _minCircleRadius = 500.0f;
		float _minCircleDistance = 20.0f;
		float _threshMult = 0.00001f;

		float _passthroughAmount = 0.0f;

		/// <summary>	
        /// The depth material for rendering depth textures.
        /// </summary>
        public Material depthMaterial
        {
            get => m_DepthMaterial;
            set => m_DepthMaterial = value;
        }

        [SerializeField]
        Material m_DepthMaterial;

		[SerializeField]
		Material _depthMaterialSmall;

		public Material _confCopyMaterial;
		
        /// <summary>
        /// Get or set the <c>AROcclusionManager</c>.
        /// </summary>
        public AROcclusionManager occlusionManager
        {
            get => m_OcclusionManager;
            set => m_OcclusionManager = value;
        }

        [SerializeField]
        [Tooltip("The AROcclusionManager which will produce depth textures.")]
        AROcclusionManager m_OcclusionManager;

        /// <summary>
        /// Get or set the <c>ARCameraManager</c>.
        /// </summary>
        public ARCameraManager cameraManager
        {
            get => m_CameraManager;
            set => m_CameraManager = value;
        }
		
		[SerializeField]
        [Tooltip("The ARSession object.")]
		ARSession m_session;
		public ARSession session
        {
            get => m_session;
            set => m_session = value;
        }
		
        [SerializeField]
        [Tooltip("The ARCameraManager which will produce camera frame events.")]
        ARCameraManager m_CameraManager;

        /// <summary>
        /// The UI Text used to display information about the image on screen.
        /// </summary>
        public TMPro.TextMeshProUGUI imageInfo
        {
            get => m_ImageInfo;
            set => m_ImageInfo = value;
        }

        [SerializeField]
        TMPro.TextMeshProUGUI m_ImageInfo;
 		
		[SerializeField]
        //TMPro.TextMeshProUGUI _debugOut;

		bool _frameTimeHit = false;

		public ComputeShader octreeShader;

		Texture2D _ourDepthV = null;
		Texture2D _ourDepthVSmall = null;
		Texture2D _ourDepthH = null;
		Texture2D _ourColorV = null;
		Texture2D _ourConfV = null;

		ComputeBuffer _rangeBuffer=null;
		
		Vector4 _viewVector = Vector4.zero;
		Vector4 _viewRight = Vector4.zero;

		static string path => Path.Combine(Application.persistentDataPath, "my_session.worldmap");

		List<GameObject> _markerList = new List<GameObject>();

		string _currPath = "";
		string _currDate = "";

		int clearTextureID = -1;
		int clearBufferID = -1;
		int depthRangeID = -1;

	#if STORE_NORMALS
		const uint TOTAL_GRID_SIZE_X = 1024;
		const uint TOTAL_GRID_SIZE_Y = 512;
		const uint TOTAL_GRID_SIZE_Z = 1024;
	#else
		const uint TOTAL_GRID_SIZE_X = 2048;
		const uint TOTAL_GRID_SIZE_Y = 1024;
		const uint TOTAL_GRID_SIZE_Z = 2048;
	#endif

		const uint GRID_SIZE_X = 32;
		const uint GRID_SIZE_Y = 16;
		const uint GRID_SIZE_Z = 32;

		const int DEPTH_WIDTH_RAW = 256;//2048;
		const int DEPTH_HEIGHT_RAW = 192;//1536;

		const int DEPTH_WIDTH = 2048;
		const int DEPTH_HEIGHT = 1536;

		const int DEPTH_WIDTH_SMALL = 512;
		const int DEPTH_HEIGHT_SMALL = 384;

		const int COLOR_WIDTH = 1920;
		const int COLOR_HEIGHT = 1440;

		const uint TOTAL_CELLS = GRID_SIZE_X * GRID_SIZE_Y * GRID_SIZE_Z;

		const uint GRID_BYTE_COUNT = TOTAL_CELLS * sizeof(ushort);
		const uint COLOR_BYTE_COUNT = TOTAL_CELLS * sizeof(uint);

		ComputeBuffer _gaussianCoeffs = null;

		int _lastCopyCount = 0;

		private Matrix4x4 _lastProjMatrix = Matrix4x4.identity;
		private Matrix4x4 _lastViewProjMatrix = Matrix4x4.identity;
        private Matrix4x4 _lastDisplayMatrix = Matrix4x4.identity;
		private Matrix4x4 _lastCamInv = Matrix4x4.identity;
		private Matrix4x4 _lastViewInverse = Matrix4x4.identity;
		
		Camera _arCamera = null;

		Vector3 _lastPosition;

		uint _totalRes = 0;
		uint _currWidth = 0;
		uint _currHeight = 0;

		bool _updateImages = true;
		bool _waitingForGrids = false;
		bool _gridsFound = false;
		bool _writingXYZ = false;
		bool _displayingCustomMessage = false;

		int _nerfImageCount = 0;

		const float SCREEN_MULTIPLIER = 1.0f;

		int _frameCount = 0;

		[SerializeField]
		ARHATSelection _selectionManager;

		GameObject _lastMarker = null;

		[SerializeField]
		TMPro.TMP_Dropdown _modeDropdown;
		
#if UNITY_IOS

		public void ClearMarkers()
		{
			for(int i = 0; i < _detectedDents.Count; ++i)
			{
				DestroyObject(_detectedDents[i]);
			}

			_detectedDents.Clear();
		}

    	public void LoadWorldMap()
    	{
        	StartCoroutine(Load());
    	}

		IEnumerator Load()
		{
			var sessionSubsystem = (ARKitSessionSubsystem)m_session.subsystem;
			if (sessionSubsystem == null)
			{
				Debug.Log("No session subsystem available. Could not load.");
				yield break;
			}

			FileStream file;
			try
			{
				file = File.Open(path, FileMode.Open);
			}
			catch (FileNotFoundException)
			{
				Debug.LogError("No ARWorldMap was found. Make sure to save the ARWorldMap before attempting to load it.");
				yield break;
			}

			Debug.Log($"Reading {path}...");

			const int bytesPerFrame = 1024 * 10;
			var bytesRemaining = file.Length;
			var binaryReader = new BinaryReader(file);
			var allBytes = new List<byte>();
			while (bytesRemaining > 0)
			{
				var bytes = binaryReader.ReadBytes(bytesPerFrame);
				allBytes.AddRange(bytes);
				bytesRemaining -= bytesPerFrame;
				yield return null;
			}

			var data = new NativeArray<byte>(allBytes.Count, Allocator.Temp);
			data.CopyFrom(allBytes.ToArray());

			Debug.Log("Deserializing to ARWorldMap...");
			if (ARWorldMap.TryDeserialize(data, out ARWorldMap worldMap))
				data.Dispose();

			if (worldMap.valid)
			{
				Debug.Log("Deserialized successfully.");
			}
			else
			{
				Debug.LogError("Data is not a valid ARWorldMap.");
				yield break;
			}

			Debug.Log("Apply ARWorldMap to current session.");
			sessionSubsystem.ApplyWorldMap(worldMap);

			StartCoroutine(LoadAnchors());
			/*foreach( a in _anchorManager.trackables)
			{
				Pose p = _anchorManager.trackables[a.trackableId].Pose;

			}*/
		}

		IEnumerator LoadAnchors()
		{
			yield return new WaitForSeconds(10f);
			//re-instantiate prefabs at the loaded anchor locations...
			//sessionSubsystem.trackables;
			
			TrackableCollection<ARAnchor> anchors =  _anchorManager.trackables;
			Debug.Log("Number of anchors: " + anchors.count);

			Vector3[] savedScales = new Vector3[anchors.count];
        	string scalePath = Path.Combine(Application.persistentDataPath + "//my_anchor_scales.txt");

        	string[] scales = System.IO.File.ReadAllLines(scalePath);

			int i = 0;
			foreach(ARAnchor a in anchors)
			{
				if(a.pending)
				{

				}
				
				GameObject anchorFab = Instantiate(_anchorPrefab);
				//anchorFab.AddComponent<ARAnchor>(a);
				anchorFab.transform.position = a.gameObject.transform.position;
				anchorFab.transform.rotation = a.gameObject.transform.rotation;
				
				string[] scaleVals = scales[i].Split(",");
            	anchorFab.transform.localScale = new Vector3(float.Parse(scaleVals[0]), float.Parse(scaleVals[1]), float.Parse(scaleVals[2]));

				_markerList.Add(anchorFab);
				//add all of these as markers in the selection manager...
				//_selectionManager.ManualMarkerAddObject(anchorFab);
				i++;
			}
		}
#endif

		void Start()
		{
			_ourDepthV = new Texture2D(DEPTH_HEIGHT, DEPTH_WIDTH, TextureFormat.RFloat, false);
			_ourDepthVSmall = new Texture2D(DEPTH_HEIGHT_RAW, DEPTH_WIDTH_RAW, TextureFormat.RFloat, false);
			_ourDepthH = new Texture2D(DEPTH_WIDTH_RAW, DEPTH_HEIGHT_RAW, TextureFormat.RFloat, false);
			_ourConfV = new Texture2D(DEPTH_HEIGHT_RAW, DEPTH_WIDTH_RAW, TextureFormat.R8, false);
			_ourColorV = new Texture2D(COLOR_HEIGHT, COLOR_WIDTH, TextureFormat.RGBA32, false);

			_arCamera = Camera.main;//m_OcclusionManager.gameObject.GetComponent<ARSessionOrigin>().camera;

			/*_flowColor = new Mat[FLOW_WINDOW_SIZE];
			for(int i = 0; i < FLOW_WINDOW_SIZE; ++i)
			{
				_flowColor[i] = new Mat(COLOR_WIDTH, COLOR_HEIGHT, CvType.CV_8UC1);
			}

			_flowTempColor = new Mat(COLOR_WIDTH, COLOR_HEIGHT, CvType.CV_8UC4);*/

			_lastPosition = _arCamera.transform.position;

			if(octreeShader != null)
			{
				if(clearTextureID == -1)
				{
					clearTextureID = octreeShader.FindKernel("CSClearTexture");
					if(clearTextureID != -1)
					{
						Debug.Log("Found clear texture");
					}
				}
				
				if(clearBufferID == -1)
				{
					clearBufferID = octreeShader.FindKernel("CSClearBuffer");
					if(clearBufferID != -1)
					{
						Debug.Log("Found clear texture");
					}
				}

				if(depthRangeID == -1)
				{
					depthRangeID = octreeShader.FindKernel("CSDepthRange");
					if(depthRangeID != -1)
					{
						Debug.Log("Found CSDepthRange shader");
					}
				}
			}

			if(_rangeBuffer == null)
			{
				_rangeBuffer = new ComputeBuffer(2, sizeof(int));
				int[] rangeData = new int[2];
				rangeData[0] = 100000;
				rangeData[1] = 0;
				_rangeBuffer.SetData(rangeData);
			}

			if(_modeDropdown != null)
			{
				_modeDropdown.onValueChanged.AddListener(delegate {
					ModeChanged(_modeDropdown);
				});	
			}

			int screenWidthMult = (int)((float)Screen.width * SCREEN_MULTIPLIER);
			int screenHeightMult = (int)((float)Screen.height * SCREEN_MULTIPLIER);

			_totalRes = (uint)_renderTargetDepthV.width * (uint)_renderTargetDepthV.height;

			_currWidth = (uint)_renderTargetDepthV.width;
			_currHeight = (uint)_renderTargetDepthV.height;

			Debug.Log(Screen.width + " " + Screen.height);

			if(_arCamera != null)
			{
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_passthroughAmount", _passthroughAmount);
			}

			Debug.Log(screenWidthMult + " " + screenHeightMult);

			//_normalsRenderTexture = new RenderTexture(screenWidthMult, screenHeightMult, 0);
			_normalsRenderTexture = new RenderTexture((int)_currWidth, (int)_currHeight, 0);
            _normalsRenderTexture.name = name + "_Normals";

            _normalsRenderTexture.enableRandomWrite = true;
            _normalsRenderTexture.filterMode = FilterMode.Point;
            _normalsRenderTexture.format = RenderTextureFormat.ARGBFloat;
            _normalsRenderTexture.useMipMap = false;
			_normalsRenderTexture.autoGenerateMips = false;
            _normalsRenderTexture.Create();

//#if USE_CPU_DEPTH
			_geometryRenderTexture = new RenderTexture((int)_currWidth, (int)_currHeight, 0);
//#else
//			_geometryRenderTexture = new RenderTexture((int)_renderTargetColorV.width, (int)_renderTargetColorV.height, 0);
//#endif
            _geometryRenderTexture.name = name + "_Geometry";

            _geometryRenderTexture.enableRandomWrite = true;
            _geometryRenderTexture.filterMode = FilterMode.Point;
            _geometryRenderTexture.format = RenderTextureFormat.ARGB32;//ARGBFloat;
            _geometryRenderTexture.useMipMap = false;
			_geometryRenderTexture.autoGenerateMips = false;
            _geometryRenderTexture.Create();
			
			//_laplacianTexture = new RenderTexture(screenWidthMult, screenHeightMult, 0);
//#if USE_CPU_DEPTH
			_laplacianTexture = new RenderTexture((int)_currWidth, (int)_currHeight, 0);
//#else
//            _laplacianTexture = new RenderTexture((int)_renderTargetColorV.width, (int)_renderTargetColorV.height, 0);
//#endif
			_laplacianTexture.name = name + "_Laplacian";

            _laplacianTexture.enableRandomWrite = true;
            _laplacianTexture.filterMode = FilterMode.Point;
            _laplacianTexture.format = RenderTextureFormat.ARGBFloat;
            _laplacianTexture.useMipMap = false;
			_laplacianTexture.autoGenerateMips = false;
            _laplacianTexture.Create();

			if(_arCamera != null)
			{
				//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_texturePoints", _normalsRenderTexture);
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_texturePoints", _laplacianTexture);
			}

			//Debug.Log(_currWidth + " " +_currHeight);

			octreeShader.SetInt("_range", (int)_kernelHalf);
			//octreeShader.SetFloat("_threshold", 0.6f);
			//octreeShader.SetFloat("_threshold", 0.95f);
			octreeShader.SetFloat("_thresholdPos", 224f * _threshMult);
			octreeShader.SetFloat("_thresholdNeg", 224f * _threshMult);

			octreeShader.SetFloat("depthWidth", (float)_currWidth);
			octreeShader.SetFloat("depthHeight", (float)_currHeight);
			octreeShader.SetFloat("depthResolution", _totalRes);
			octreeShader.SetInt("orientation", (int)Screen.orientation);
			octreeShader.SetInt("screenWidth", screenWidthMult);
			octreeShader.SetInt("screenHeight", screenHeightMult);
			octreeShader.SetInt("volumeOffset", 0);
			octreeShader.SetInt("totalCells", (int)TOTAL_CELLS);
			octreeShader.SetInt("computeMaxEdgeSize", 256);

			octreeShader.SetTexture(clearTextureID, "geometryTexture", _geometryRenderTexture);
			octreeShader.SetTexture(clearTextureID, "laplacianTexture", _laplacianTexture);
			octreeShader.SetTexture(clearTextureID, "depthTexture", _renderTargetDepthV);
			//octreeShader.SetTexture(clearTextureID, "colorTexture", _renderTargetColorV);

			//octreeShader.SetTexture(textureID, "renderTexture", _pointRenderTexture);
			
			//setup render all points...

			//setup depth range.
			//octreeShader.SetTexture(depthRangeID, "depthTexture", _renderTargetDepthV);
			octreeShader.SetTexture(depthRangeID, "depthTexture", _normalsRenderTexture);
			octreeShader.SetBuffer(depthRangeID, "rangeBuf", _rangeBuffer);

			_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_EnvironmentConf", _renderTargetConfVUnrotated);

			octreeShader.SetFloat("_threshold", _threshold * _threshMult);
			octreeShader.SetFloat("_distanceCutoff", 0.00635f);
			//octreeShader.SetFloat("_distanceCutoff",0.0127f);

			_currDate = DateTime.Now.ToString("M_dd_yyyy_hh_mm_ss");
			LogDebug();
		}

		void LogDebug()
		{
			//_debugOut.text = "KH: " + _kernelHalf.ToString("F2") + " DM: " + _depthMult.ToString("F2") + " DMax: " + _threshold.ToString("F2") + " NB: " + 
			//_numBlurs.ToString("F2") + " minC: " + _minCircleRadius.ToString("F2") + " maxC: " + _maxCircleRadius.ToString("F2") + " minD: " + _minCircleDistance.ToString("F2");
		}

        void Awake()
        {
#if UNITY_ANDROID
            k_AndroidFlipYMatrix[1,1] = -1.0f;
            k_AndroidFlipYMatrix[2,1] = 1.0f;
#endif // UNITY_ANDROID
			Screen.orientation = ScreenOrientation.Portrait;
        }

        void OnEnable()
        {
            // Subscribe to the camera frame received event, and initialize the display rotation matrix.
            Debug.Assert(m_CameraManager != null, "no camera manager");
            m_CameraManager.frameReceived += OnCameraFrameEventReceived;
			m_OcclusionManager.frameReceived += OnOcclusionFrameEventReceived;
            m_DisplayRotationMatrix = Matrix4x4.identity;

            // When enabled, get the current screen orientation, and update the raw image UI.
            m_CurrentScreenOrientation = Screen.orientation;
            //UpdateRawImage();
        }
		
		public void AddAnchor()
		{
			if(_lastMarker != null)
			{
				/*if(_anchorManager != null)
				{
					if(_slider != null)
					{
						_slider.value = 0.0375f;
					}
					//Debug.Log("Adding anchor");
					//_anchorManager.AddAnchor(new Pose(_lastMarker.transform.position, _lastMarker.transform.rotation));
				}*/

				_markerList.Add(_lastMarker);
			}
		}

        void OnDisable()
        {
            // Unsubscribe to the camera frame received event, and initialize the display rotation matrix.
            Debug.Assert(m_CameraManager != null, "no camera manager");
            m_CameraManager.frameReceived -= OnCameraFrameEventReceived;
			m_OcclusionManager.frameReceived -= OnOcclusionFrameEventReceived;
            m_DisplayRotationMatrix = Matrix4x4.identity;
        }

		public void SetNumBlurs(float numBlurs)
		{
			_numBlurs = (int)numBlurs;
			LogDebug();
		}

		public void SetThreshold(float val)
		{
			octreeShader.SetFloat("_threshold", val * _threshMult);
			_threshold = val;
			LogDebug();
		}

		public void SetPosThreshold(float val)
		{
			octreeShader.SetFloat("_thresholdPos", val * _threshMult);
			_thresholdNeg = val;
			LogDebug();
		}

		public void SetNegThreshold(float val)
		{
			octreeShader.SetFloat("_thresholdNeg", val * _threshMult);
			_thresholdPos = val;
			LogDebug();
		}

		public void SetThresholdMult(float val)
		{
			_threshMult = val;
			octreeShader.SetFloat("_thresholdPos", _thresholdPos * _threshMult);
			octreeShader.SetFloat("_thresholdNeg", _thresholdNeg * _threshMult);
			octreeShader.SetFloat("_threshold", _threshold * _threshMult);
		}

		public void SetDistanceCutoff(float val)
		{
			octreeShader.SetFloat("_distanceCutoff", val);
			octreeShader.SetFloat("_depthMult", val);
			_depthMult = val;
			LogDebug();
		}

		public void SetRange(float val)
		{
			_kernelHalf = val;
			octreeShader.SetInt("_range", (int)val);

			LogDebug();
		}

		public void SetPassthrough(float val)
		{
			_passthroughAmount = val;
		}

		public void SetMinCircle(float val)
		{
			_minCircleRadius = val;
			LogDebug();
		}

		public void SetMaxCircle(float val)
		{
			_maxCircleRadius = val;
			LogDebug();
		}

		public void SetMinCircleDistance(float val)
		{
			_minCircleDistance = val;
			LogDebug();
		}

		void OnDestroy()
		{

		}
		

		void ModeChanged(TMPro.TMP_Dropdown change)
		{
			Debug.Log("The new mode is: " + change.value);

			_currentMode = (AppMode)change.value;
		}

		void SetLastValues()
		{
			_lastExposure = _thisExposure;
			_lastIntensity = _thisIntensity;
			_lastColorTemp = _thisColorTemp;		
		}

		void UpdateCameraParams()
		{
			var cameraParams = new XRCameraParams {
				zNear = _arCamera.nearClipPlane,
				zFar = _arCamera.farClipPlane,
				screenWidth = Screen.width,//_currWidth,//
				screenHeight = Screen.height,//_currHeight,//
				screenOrientation = Screen.orientation
			};

			//Debug.Log(_lastDisplayMatrix.ToString("F4"));

			Matrix4x4 viewMatrix = Matrix4x4.identity;//_arCamera.viewMatrix;
			Matrix4x4 projMatrix = _lastProjMatrix;
			Matrix4x4 viewInverse = Matrix4x4.identity;

			if (m_CameraManager.subsystem.TryGetLatestFrame(cameraParams, out var cameraFrame)) {
				viewMatrix = Matrix4x4.TRS(_arCamera.transform.position, _arCamera.transform.rotation, Vector3.one).inverse;
				if (SystemInfo.usesReversedZBuffer)
				{
					viewMatrix.m20 = -viewMatrix.m20;
					viewMatrix.m21 = -viewMatrix.m21;
					viewMatrix.m22 = -viewMatrix.m22;
					viewMatrix.m23 = -viewMatrix.m23;

					projMatrix.m20 = -projMatrix.m20;
					projMatrix.m21 = -projMatrix.m21;
					projMatrix.m22 = -projMatrix.m22;
					projMatrix.m23 = -projMatrix.m23;
				}
				projMatrix = cameraFrame.projectionMatrix;
				viewInverse = viewMatrix.inverse;
			}
			
			Matrix4x4 flipYZ = new Matrix4x4();
			flipYZ.SetRow(0, new Vector4(1f,0f,0f,0f));
			flipYZ.SetRow(1, new Vector4(0f,1f,0f,0f));
			flipYZ.SetRow(2, new Vector4(0f,0f,-1f,0f));
			flipYZ.SetRow(3, new Vector4(0f,0f,0f,1f));

			//the way we are making this quaternion is impacting the correctness (i.e. we need to do it eventhough the angle is zero, for things to work)
			
			//Debug.Log(viewMatrix.ToString("F6"));
			
			Matrix4x4 rotateToARCamera = flipYZ;

			Matrix4x4 theMatrix = viewInverse * rotateToARCamera;
			_lastViewInverse = theMatrix;

			Matrix4x4 camIntrinsics = Matrix4x4.identity;

			Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;//theMatrix.inverse;//Matrix4x4.TRS(_arCamera.transform.position, _arCamera.transform.rotation, Vector3.one);//_arCamera.worldToCameraMatrix;//
			//Debug.Log(viewProjMatrix.ToString());
			_lastViewProjMatrix = viewProjMatrix;

			if (!m_CameraManager.subsystem.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
			{
				SetLastValues();
				return;
			}

			if(_bIsCapturing && !_bDoneCapturing)
			{
				if(_markerList.Count > 0)
				{
					string pl = "";
					
					//Debug.Log("Marker count: " + _markerList.Count);
					int numLabels = _markerList.Count / 4;
					bool[] numLabelsPassed = new bool[numLabels];
					for(int i = 0; i < numLabels; ++i)
					{
						numLabelsPassed[i] = true;
					}

					for(int i = 0; i < _markerList.Count; ++i)
					{
						Vector4 v = Vector4.zero;
						v.x = _markerList[i].transform.position.x;
						v.y = _markerList[i].transform.position.y;
						v.z = _markerList[i].transform.position.z;
						v.w = 1.0f;

						v = viewProjMatrix * v;
						v.x /= v.w;
						v.y /= v.w;
						v.z /= v.w;
						v.w = 1.0f;

						v.x = (v.x + 1.0f) * 0.5f;
						v.y = (v.y + 1.0f) * 0.5f;
						v.z = (v.z + 1.0f) * 0.5f;

						if(v.x < 0.0f || v.y < 0.0f || v.x > 1.0f || v.y > 1.0f)
						{
							numLabelsPassed[i/4] = false;
						}
					}

					for(int i = 0; i < _markerList.Count; ++i)
					{
						if(numLabelsPassed[i/4])
						{
							if(pl.Length == 0)
							{
								pl = "0 ";
							}

							Vector4 v = Vector4.zero;
							v.x = _markerList[i].transform.position.x;
							v.y = _markerList[i].transform.position.y;
							v.z = _markerList[i].transform.position.z;
							v.w = 1.0f;

							v = viewProjMatrix * v;
							v.x /= v.w;
							v.y /= v.w;
							v.z /= v.w;
							v.w = 1.0f;

							v.x = (v.x + 1.0f) * 0.5f;
							v.y = (v.y + 1.0f) * 0.5f;
							v.z = (v.z + 1.0f) * 0.5f;

							v.x *= COLOR_WIDTH;//_currWidth;
							v.y *= COLOR_HEIGHT;///_currHeight;

							pl = pl + (v.x/COLOR_WIDTH).ToString("F4") + " " + ((COLOR_HEIGHT - v.y)/COLOR_HEIGHT).ToString("F4");
							if((i % 4) == 3)
							{
								pl = pl + "\n";
								if(i != _markerList.Count-1)
								{
									pl = pl + "0 ";
								}
							}
							else
							{
								pl = pl + " ";
							}
						}
					}

					if(pl.Length > 0)
					{
						string filenameTxt2 = _opticalFlowWriteCount.ToString("D4")+"color.txt";
						System.IO.File.WriteAllText(System.IO.Path.Combine(_currPath, filenameTxt2), pl);
						string filenameTxt3 = _opticalFlowWriteCount.ToString("D4")+"flow.txt";
						System.IO.File.WriteAllText(System.IO.Path.Combine(_currPath, filenameTxt3), pl);
					}
				}
			}

			//we want to pass in the data to compute buffers and calculate that way..
			//viewProjMatrix = projMatrix * theMatrix.inverse;
			//Debug.Log(_lastDisplayMatrix.ToString("F4"));

			octreeShader.SetMatrix("localToWorld", theMatrix);
			octreeShader.SetMatrix("displayMatrix", _lastDisplayMatrix);
			octreeShader.SetMatrix("viewProjMatrix", viewProjMatrix);

			//Debug.Log(cameraIntrinsics.focalLength.x + " " + cameraIntrinsics.focalLength.y + " " + cameraIntrinsics.principalPoint.x + " " + cameraIntrinsics.principalPoint.y);
			//these could be set on re-orientation...
			//focal length values are equal
			camIntrinsics.SetColumn(0, new Vector4(cameraIntrinsics.focalLength.y, 0f, 0f, 0f));
			camIntrinsics.SetColumn(1, new Vector4(0f, cameraIntrinsics.focalLength.x, 0f, 0f));
			camIntrinsics.SetColumn(2, new Vector4(cameraIntrinsics.principalPoint.y, cameraIntrinsics.principalPoint.x, 1f, 0f));

			Matrix4x4 camInv = camIntrinsics.inverse;
			_lastCamInv = camInv;

			octreeShader.SetMatrix("camIntrinsicsInverse", camInv);

			if(_currentMode == AppMode.eHAIL)
			{
				if(_arCamera != null)
				{
					//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_texturePoints", _normalsRenderTexture);
					//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_texturePoints", _laplacianTexture);
					//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_texturePoints", _geometryRenderTexture);

					_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetTexture("_ourDepth", _renderTargetDepthVSmall);
					_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_passthroughAmount", _passthroughAmount);

				}
				//Debug.Log(camInv.ToString("F4"));
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetMatrix("_camIntrinsicsInverse", camInv);
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetMatrix("_localToWorld", theMatrix);
			}
			else
			{
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_isHailMode", 0.0f);
			}
		}

		void LateUpdate()
		{
			if(_arCamera != null)
			{
				octreeShader.SetVector("camPos", _arCamera.transform.position);
			}

			if(ARSession.notTrackingReason != NotTrackingReason.None)
			{
				_frameTimeHit = false;
				return;
			}
			
			//Debug.Log("LATE UPDATE: " + _frameCount);

			if(ARSession.state != ARSessionState.SessionTracking)
			{
				//re-show screen space UI?  maybe this happens automatically...
				_frameTimeHit = false;
				return;
			}

			if(_currentMode == AppMode.eHAIL)
			{
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_isScanning", 0.0f);
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_isHailMode", 1.0f);

				octreeShader.Dispatch(clearTextureID, ((int)_currWidth + 31) / 32, ((int)_currHeight + 31) / 32, 1);
			
			}

			if(_currentMode == AppMode.eHAIL)
			{
				_updateImages = true;

				if(_doOpticalFlow)
				{
					if(_captureOnce && _bIsCapturing)
					{
						//if(((_opticalFlowCount == (FLOW_WINDOW_SIZE/2))))	
						if(_opticalFlowCount < FLOW_WINDOW_SIZE)
						{
							UpdateCameraParams();
						}
					}
					else
					{
						if(((_frameCount % (FLOW_WINDOW_SIZE/2)) == 0))
						{
							UpdateCameraParams();
						}
					}
				}
				else
				{
					UpdateCameraParams();
				}

				SetLastValues();
			}
			
			if(_currentMode == AppMode.eHAIL)
			{
				if(_bIsCapturing && _captureOnce)
				{
					if(_doOpticalFlow && _frameTimeHit)
					{
						_frameTimeHit = false;
					}
					
					if(_doOpticalFlow)
					{
						if(_bCapturedColor && _bCapturedDepth)
						{
							_bIsCapturing = false;
							_frameTimeHit = false;
							_bCapturedColor = false;
							_bCapturedDepth = false;
							_bDoneCapturing = true;

							Debug.Log("Done with flow capture");
						}
					}
					else
					{
						if(_bCapturedColor && _bCapturedDepth)
						{
							_bIsCapturing = false;
							_frameTimeHit = false;
							_bCapturedColor = false;
							_bCapturedDepth = false;
							_bDoneCapturing = true;

							Debug.Log("Done with capture");
						}
					}
				}
			}

			_lastPosition = _arCamera.transform.position;
		}

        void Update()
        {
			//Debug.Log("UPDATE");
            // If we are on a device that does supports neither human stencil, human depth, nor environment depth,
            // display a message about unsupported functionality and return.
            Debug.Assert(m_OcclusionManager != null, "no occlusion manager");

			if (m_OcclusionManager.descriptor?.environmentDepthImageSupported == 0)
			{
				LogText("Environment depth is not supported on this device.");
			}
			/*else
			{
				// Get all of the occlusion textures.
				Texture2D envDepth = m_OcclusionManager.environmentDepthTexture;

				// Display some text information about each of the textures.
				m_StringBuilder.Clear();
				BuildTextureInfo(m_StringBuilder, "env", envDepth);
				LogText(m_StringBuilder.ToString());
			}*/

			if(_arCamera != null)
			{
				_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_isScanning", (_bIsCapturing || _bDoneCapturing) ? 1.0f : 0.0f);

			}

			if(_bIsCapturing)
			{
				if(_bDoneCapturing)
				{
					LogText("State: Reviewing");
				}
				else
				{
					if(!_displayingCustomMessage)
					{
						LogText("State: Scanning");
					}
				}
			}
			else
			{
				if(!_displayingCustomMessage)
				{
					LogText("State: Tracking");
				}
			}

			if(_selectionManager != null)
			{
				GameObject marker = null;
				if(_selectionManager.TryTouchMoveMarker(128, out marker))
				{
					if(marker != null)
					{
						Vector2 vTest = Vector2.zero;
						_selectionManager.TryGetEnhanced(out vTest);
						_lastMarker = marker;
						//Debug.Log("Press point: " + vTest.ToString("F1"));

						//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetVector("_PressPoint", new Vector4(vTest.x, vTest.y, 0.0f, 0.0f));
						//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetVector("_WindowSize", _windowSize);
						//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_PressOffset", _pressOffset);
					}
				}
				else
				{
					//_lastMarker = null;

					//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetVector("_PressPoint", Vector4.zero);
					//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetVector("_WindowSize", _windowSize);
					//_arCamera.GetComponent<ARCameraBackground>().customMaterial.SetFloat("_PressOffset", _pressOffset);
				}
			}
        }
		
		void DisplayCustomMessage(string msg)
		{
			if(!_displayingCustomMessage)
			{
				StartCoroutine(DisplayMessageForTime(msg, 1f));
			}
		}

		IEnumerator DisplayMessageForTime(string msg, float timeAmt)
		{
			_displayingCustomMessage = true;

			LogText(msg);

			yield return new WaitForSeconds(timeAmt);

			_displayingCustomMessage = false;
		}

		void UpdateDepthConf()
		{
			RenderTexture currentActiveRT = RenderTexture.active;
			
			if(m_OcclusionManager != null)
			{
				//environment depth image is RFloat format
				if(m_OcclusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage imageDepth))
				{
					//256x192
					byte[] depthData = imageDepth.GetPlane(0).data.ToArray();
#if WRITE_FRAMES
					if(_currentMode == AppMode.eHAIL && _bIsCapturing && !_bDoneCapturing)
					{
						//Debug.Log("OCCLUSION FRAME");
#if DIRECT_DEPTH_WRITE
						//convert and write out this depth data...
						string fName = _opticalFlowWriteCount.ToString("D4")+"depthLo.bytes";
						File.WriteAllBytes(System.IO.Path.Combine(_currPath, fName), depthData);
#endif
					}
#endif
					
					Vector2Int outputDimensions = imageDepth.dimensions;

					//set the data
					//this is laid out 256x192, but we render it out to ourDepthV which is 192x256...
					//_depthBuffer.SetData(depthData);
					_ourDepthH.LoadRawTextureData(depthData);
					_ourDepthH.Apply();

					//if we can render this depth to the _renderTargetDepthV, that would be ideal...
					var commandBuffer = new UnityEngine.Rendering.CommandBuffer();
					commandBuffer.name = "Env Depth Blit Pass";

					//Debug.Log(outputDimensions.ToString());
					m_DepthMaterial.SetTexture("_MainTex", _ourDepthH);
					//m_DepthMaterial.SetBuffer("_depthBuffer", _depthBuffer);
					m_DepthMaterial.SetFloat("_depthWidth", (float)outputDimensions.y);
					m_DepthMaterial.SetFloat("_depthHeight", (float)outputDimensions.x);
					m_DepthMaterial.SetInt("_Orientation", (int)m_CurrentScreenOrientation);

					//RenderTexture.active = _renderTargetDepthV;
					//commandBuffer.SetRenderTarget(_renderTargetDepthV);
					Graphics.SetRenderTarget(_renderTargetDepthVSmall.colorBuffer, _renderTargetDepthVSmall.depthBuffer);
					commandBuffer.ClearRenderTarget(false, true, Color.black);
					//shouldn't pass envDepth here... if we don't set use_cpu_depth...
					commandBuffer.Blit(_ourDepthH, /*_renderTargetDepthV*/UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive, m_DepthMaterial);
					//commandBuffer.Blit(envDepth, /*_renderTargetDepthV*/UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive, m_DepthMaterial);
					Graphics.ExecuteCommandBuffer(commandBuffer);
					
				}

				Texture2D envDepth = m_OcclusionManager.environmentDepthTexture;
				if(envDepth != null)
				{
					
					//Debug.Log("Width: " + envDepth.width);	//2048!?!?!
					//Debug.Log("Height: " + envDepth.height);	//1536!?!?
					//Debug.Log("Format: " + envDepth.graphicsFormat);	R32_SFloat
					//Debug.Log("Mip map count: " + envDepth.mipmapCount);	12
					//Debug.Log(lastDisplayMatrix.ToString("F6"));
					
					var commandBuffer = new UnityEngine.Rendering.CommandBuffer();
					commandBuffer.name = "Env Depth Blit Pass";

					m_DepthMaterial.SetTexture("_MainTex", envDepth);
					m_DepthMaterial.SetInt("_Orientation", (int)m_CurrentScreenOrientation);

					//RenderTexture.active = _renderTargetDepthV;
					//commandBuffer.SetRenderTarget(_renderTargetDepthV);
					Graphics.SetRenderTarget(_renderTargetDepthV.colorBuffer, _renderTargetDepthV.depthBuffer);
					commandBuffer.ClearRenderTarget(false, true, Color.black);
					commandBuffer.Blit(envDepth, /*_renderTargetDepthV*/UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive, m_DepthMaterial);
					Graphics.ExecuteCommandBuffer(commandBuffer);
					
					//Graphics.Blit(envDepth, _renderTargetDepthV, m_DepthMaterial);
					//Graphics.CopyTexture(envDepth, _renderTargetDepth);

					//write out binary depth here...
					if(_currentMode == AppMode.eHAIL && _bIsCapturing && !_bDoneCapturing)
					{
						//Debug.Log("OCCLUSION FRAME2");
						string fName = _opticalFlowWriteCount.ToString("D4")+"depthHi.bytes";
						_ourDepthV.ReadPixels(new UnityEngine.Rect(0, 0, _ourDepthV.width, _ourDepthV.height), 0, 0, false);
						_ourDepthV.Apply();

#if WRITE_FRAMES
						byte[] depthT = _ourDepthV.GetRawTextureData();
						File.WriteAllBytes(System.IO.Path.Combine(_currPath, fName), depthT);
						
						Matrix4x4 viewMatrix = Matrix4x4.TRS(_arCamera.transform.position, _arCamera.transform.rotation, Vector3.one).inverse;

						if (!m_CameraManager.subsystem.TryGetIntrinsics(out XRCameraIntrinsics cameraIntrinsics))
						{
							SetLastValues();
							return;
						}
						
						Matrix4x4 camIntrinsics = Matrix4x4.identity;
						camIntrinsics.SetColumn(0, new Vector4(cameraIntrinsics.focalLength.y, 0f, 0f, 0f));
						camIntrinsics.SetColumn(1, new Vector4(0f, cameraIntrinsics.focalLength.x, 0f, 0f));
						camIntrinsics.SetColumn(2, new Vector4(cameraIntrinsics.principalPoint.y, cameraIntrinsics.principalPoint.x, 1f, 0f));

						Matrix4x4 camInv = camIntrinsics.inverse;

						Matrix4x4 viewInverse = viewMatrix.inverse;
						Matrix4x4 flipYZ = new Matrix4x4();
						flipYZ.SetRow(0, new Vector4(1f,0f,0f,0f));
						flipYZ.SetRow(1, new Vector4(0f,1f,0f,0f));
						flipYZ.SetRow(2, new Vector4(0f,0f,-1f,0f));
						flipYZ.SetRow(3, new Vector4(0f,0f,0f,1f));

						Matrix4x4 theMatrix = viewInverse * flipYZ;
						Matrix4x4 viewProj = _lastProjMatrix * viewMatrix;

						string depthString = theMatrix[0].ToString("F4") + " " + theMatrix[4].ToString("F4") + " " + theMatrix[8].ToString("F4") + " " + theMatrix[12].ToString("F4") + "\n";
						depthString = depthString + (theMatrix[1].ToString("F4") + " " + theMatrix[5].ToString("F4") + " " + theMatrix[9].ToString("F4") + " " + theMatrix[13].ToString("F4") + "\n");
						depthString = depthString + (theMatrix[2].ToString("F4") + " " + theMatrix[6].ToString("F4") + " " + theMatrix[10].ToString("F4") + " " + theMatrix[14].ToString("F4") + "\n");
						depthString = depthString + (theMatrix[3].ToString("F4") + " " + theMatrix[7].ToString("F4") + " " + theMatrix[11].ToString("F4") + " " + theMatrix[15].ToString("F4") + "\n");
						
						string filenameTxt = _opticalFlowWriteCount.ToString("D4")+"_trans.txt";
						System.IO.File.WriteAllText(System.IO.Path.Combine(_currPath, filenameTxt), depthString);	
						
						string depthString2 = camInv[0].ToString("F4") + " " + camInv[4].ToString("F4") + " " + camInv[8].ToString("F4") + " " + camInv[12].ToString("F4") + "\n";
						depthString2 = depthString2 + (camInv[1].ToString("F4") + " " + camInv[5].ToString("F4") + " " + camInv[9].ToString("F4") + " " + camInv[13].ToString("F4") + "\n");
						depthString2 = depthString2 + (camInv[2].ToString("F4") + " " + camInv[6].ToString("F4") + " " + camInv[10].ToString("F4") + " " + camInv[14].ToString("F4") + "\n");
						depthString2 = depthString2 + (camInv[3].ToString("F4") + " " + camInv[7].ToString("F4") + " " + camInv[11].ToString("F4") + " " + camInv[15].ToString("F4") + "\n");
						
						string filenameTxt2 = _opticalFlowWriteCount.ToString("D4")+"_camInv.txt";
						System.IO.File.WriteAllText(System.IO.Path.Combine(_currPath, filenameTxt2), depthString2);

						string depthString3 = viewProj[0].ToString("F4") + " " + viewProj[4].ToString("F4") + " " + viewProj[8].ToString("F4") + " " + viewProj[12].ToString("F4") + "\n";
						depthString3 = depthString3 + (viewProj[1].ToString("F4") + " " + viewProj[5].ToString("F4") + " " + viewProj[9].ToString("F4") + " " + viewProj[13].ToString("F4") + "\n");
						depthString3 = depthString3 + (viewProj[2].ToString("F4") + " " + viewProj[6].ToString("F4") + " " + viewProj[10].ToString("F4") + " " + viewProj[14].ToString("F4") + "\n");
						depthString3 = depthString3 + (viewProj[3].ToString("F4") + " " + viewProj[7].ToString("F4") + " " + viewProj[11].ToString("F4") + " " + viewProj[15].ToString("F4") + "\n");
						
						string filenameTxt3 = _opticalFlowWriteCount.ToString("D4")+"_viewProj.txt";
						System.IO.File.WriteAllText(System.IO.Path.Combine(_currPath, filenameTxt3), depthString3);

						//Debug.Log("Wrote data: " + _opticalFlowCount);
#endif
					} 
				}

				Texture2D envConf = m_OcclusionManager.environmentDepthConfidenceTexture;
				if(envConf != null)
				{
					
					//Debug.Log("Width: " + envConf.width);	//256
					//Debug.Log("Height: " + envConf.height);	//192
					//Debug.Log("Format: " + envConf.graphicsFormat);	//R8_UNorm
					//Debug.Log("Mip map count: " + envConf.mipmapCount);	//1

					_confCopyMaterial.SetTexture("_MainTex", envConf);
					_confCopyMaterial.SetInt("_Orientation", (int)m_CurrentScreenOrientation);

					//RenderTexture.active = _renderTargetConfV;
					var commandBuffer = new UnityEngine.Rendering.CommandBuffer();
					commandBuffer.name = "Env Conf Blit Pass";
					//commandBuffer.SetRenderTarget(_renderTargetConfV);
					Graphics.SetRenderTarget(_renderTargetConfV.colorBuffer, _renderTargetConfV.depthBuffer);
					commandBuffer.ClearRenderTarget(false, true, Color.black);
					commandBuffer.Blit(envConf, /*_renderTargetConfV*/UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive, _confCopyMaterial);
					Graphics.ExecuteCommandBuffer(commandBuffer);

#if WRITE_FRAMES
					//write this differently when using CPU images
					if(_currentMode == AppMode.eHAIL && _bIsCapturing && !_bDoneCapturing)
					{
						//Debug.Log("OCCLUSION FRAME3");
						string fName = _opticalFlowWriteCount.ToString("D4")+"conf.png";
						_ourConfV.ReadPixels(new UnityEngine.Rect(0, 0, _ourConfV.width, _ourConfV.height), 0, 0, false);
						_ourConfV.Apply();
						File.WriteAllBytes(System.IO.Path.Combine(_currPath, fName), ImageConversion.EncodeArrayToPNG(_ourConfV.GetRawTextureData(), UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, (uint)_ourConfV.width, (uint)_ourConfV.height));
						_nerfImageCount++;
					}
#endif			
					//Graphics.Blit(envConf, _renderTargetConfV, _confCopyMaterial);
					//Graphics.CopyTexture(envConf, _renderTargetConf);

					var commandBuffer2 = new UnityEngine.Rendering.CommandBuffer();
					commandBuffer2.name = "Env Conf Blit Pass Unrotated";
					//commandBuffer2.SetRenderTarget(_renderTargetConfVUnrotated);
					Graphics.SetRenderTarget(_renderTargetConfVUnrotated.colorBuffer, _renderTargetConfVUnrotated.depthBuffer);
					//RenderTexture.active = _renderTargetConfVUnrotated;
					commandBuffer2.ClearRenderTarget(false, true, Color.black);
					commandBuffer2.Blit(envConf, /*_renderTargetConfVUnrotated*/UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive, _confUnrotatedCopyMaterial);
					Graphics.ExecuteCommandBuffer(commandBuffer2);
					
					//Graphics.Blit(envConf, _renderTargetConfVUnrotated, _confUnrotatedCopyMaterial);
					//Graphics.CopyTexture(envConf, _renderTargetConfVUnrotated);

					//commandBuffer.Dispose();

					imageDepth.Dispose();
				}
			}

			if(currentActiveRT != null)
			{
				Graphics.SetRenderTarget(currentActiveRT.colorBuffer, currentActiveRT.depthBuffer);
			}
			else
			{
				RenderTexture.active = null;
			}

			if(_captureOnce && _bIsCapturing)
			{
				//Debug.Log("Captured depth");
				_bCapturedDepth = true;
			}
		}

		void OnOcclusionFrameEventReceived(AROcclusionFrameEventArgs cameraFrameEventArgs)
		{
			if(m_OcclusionManager != null)
			{
				if(!_updateImages)
				{
					return;	
				}

				if(!_frameTimeHit)
				{
					return;
				}

				UpdateDepthConf();
			}
		}

        /// <summary>
        /// When the camera frame event is raised, capture the display rotation matrix.
        /// </summary>
        /// <param name="cameraFrameEventArgs">The arguments when a camera frame event is raised.</param>
        void OnCameraFrameEventReceived(ARCameraFrameEventArgs cameraFrameEventArgs)
        {
			_frameCount++;
			//Debug.Log("CAMERA FRAME: " + _frameCount);
			if(cameraFrameEventArgs.exposureOffset.HasValue)
				_thisExposure = (float)cameraFrameEventArgs.exposureOffset.Value;
			
			if(cameraFrameEventArgs.lightEstimation.averageIntensityInLumens.HasValue)
				_thisIntensity = cameraFrameEventArgs.lightEstimation.averageIntensityInLumens.Value;
			
			_lastCameraGrain = cameraFrameEventArgs.noiseIntensity;

			if(cameraFrameEventArgs.lightEstimation.averageColorTemperature.HasValue)
				_lastColorTemp = cameraFrameEventArgs.lightEstimation.averageColorTemperature.Value;

			float eo = _thisExposure / _lastExposure;
			float ai = _thisIntensity;

			if((eo < 0.985 || eo > 1.015 || ai < 800 || ai > 1200))
            {
				if(ai > 1200)
				{
					DisplayCustomMessage("State: Too bright");
				}
				else if(ai < 800)
				{
					DisplayCustomMessage("State: Too dim");
				}
				else if(eo < 0.985)
				{
					DisplayCustomMessage("State: Dark change");
				}
				else if(eo > 1.015)
				{
					DisplayCustomMessage("State: Bright change");
				}
				SetLastValues();
				return;
			}

			if(_currentMode == AppMode.eHAIL)
			{
				if(_doOpticalFlow  && ((_opticalFlowCount == (FLOW_WINDOW_SIZE/2)-1)))
				{
					if(_bIsCapturing && _captureOnce)
					{
						//Debug.Log("Optical flow count: " + _opticalFlowCount);
						//Debug.Log("Nerf count: " + _nerfImageCount);
						_frameTimeHit = true;
					}
				}
				else if(!_doOpticalFlow)
				{
					_frameTimeHit = true;
				}
			}
			
			if(!_updateImages)
			{
				return;	
			}
			
			if (cameraFrameEventArgs.projectionMatrix.HasValue)
                _lastProjMatrix = cameraFrameEventArgs.projectionMatrix.Value;
            
			if (cameraFrameEventArgs.displayMatrix.HasValue)
                _lastDisplayMatrix = cameraFrameEventArgs.displayMatrix.Value;
			
			//if(cameraFrameEventArgs.lightEstimation.colorCorrection.HasValue)
			//	_lastCorrection = cameraFrameEventArgs.lightEstimation.colorCorrection.Value;
#if UNITY_ANDROID
			//need to handle android differently 3 textures on different "planes" y, u, v...
			Texture2D Y = cameraFrameEventArgs.textures[0];
			//Debug.Log(Y.format);
			if(Y != null)
#else
			Texture2D Y = cameraFrameEventArgs.textures[0];
			Texture2D CbCr = cameraFrameEventArgs.textures[1];
			if(Y != null && CbCr != null)
#endif
			{
				RenderTexture currentActiveRT = RenderTexture.active;

				//Debug.Log("Width: " + Y.width);	//256
				//Debug.Log("Height: " + Y.height);	//192
				//Debug.Log("Format: " + envConf.graphicsFormat);	//R8_UNorm
				//Debug.Log("Mip map count: " + envConf.mipmapCount);	//1
				_colorCopyMaterial.SetTexture("_MainTex", Y);
#if UNITY_ANDROID
#else
				_colorCopyMaterial.SetTexture("_SecondTex", CbCr);
#endif
				_colorCopyMaterial.SetInt("_Orientation", (int)m_CurrentScreenOrientation);

				var commandBuffer = new UnityEngine.Rendering.CommandBuffer();
				commandBuffer.name = "Color Blit Pass";

				//RenderTexture.active = _renderTargetColorV;
				Graphics.SetRenderTarget(_renderTargetColorV.colorBuffer,_renderTargetColorV.depthBuffer);
				//commandBuffer.SetRenderTarget(_renderTargetColorV);
				commandBuffer.ClearRenderTarget(false, true, Color.black);
				commandBuffer.Blit(Y, /*_renderTargetColorV*/UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive, _colorCopyMaterial);
				Graphics.ExecuteCommandBuffer(commandBuffer);
				
				//Graphics.Blit(Y, _renderTargetColorV, _colorCopyMaterial);
				
				//if in NERF capture mode, write out the texture... 
				if(_currentMode == AppMode.eHAIL && _doOpticalFlow)
				{
#if WRITE_FRAMES
					if(_bIsCapturing && !_bDoneCapturing)
					{
						_ourColorV.ReadPixels(new UnityEngine.Rect(0, 0, _ourColorV.width, _ourColorV.height), 0, 0, false);
						_ourColorV.Apply();

						//Utils.fastTexture2DToMat(_ourColorV, _flowTempColor);
						//Imgproc.cvtColor(_flowTempColor, _flowColor[_frameCount%FLOW_WINDOW_SIZE], Imgproc.COLOR_BGRA2GRAY);

						//Debug.Log("CAMERA FRAME");
						string fName = (_opticalFlowWriteCount).ToString("D4")+"color.png";
						File.WriteAllBytes(System.IO.Path.Combine(_currPath, fName), ImageConversion.EncodeArrayToPNG(_ourColorV.GetRawTextureData(), 
							UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)_ourColorV.width, (uint)_ourColorV.height));
						
						//Debug.Log("Wrote flow color: " + _opticalFlowCount);
						_opticalFlowCount++;
						_opticalFlowWriteCount++;
					}
#endif
				}
				else if((_currentMode == AppMode.eHAIL) && _bIsCapturing && !_bDoneCapturing)
				{
#if WRITE_FRAMES
					string fName = _nerfImageCount.ToString("D4")+"color.png";

					_ourColorV.ReadPixels(new UnityEngine.Rect(0, 0, _ourColorV.width, _ourColorV.height), 0, 0, false);
					_ourColorV.Apply();

					File.WriteAllBytes(System.IO.Path.Combine(_currPath, fName), ImageConversion.EncodeArrayToPNG(_ourColorV.GetRawTextureData(), 
						UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)_ourColorV.width, (uint)_ourColorV.height));
#endif
				}

				if(currentActiveRT != null)
				{
					Graphics.SetRenderTarget(currentActiveRT.colorBuffer, currentActiveRT.depthBuffer);
				}
				else
				{
					RenderTexture.active = null;
				}

				if(_captureOnce && _bIsCapturing)
				{
					if(_doOpticalFlow)
					{
						//Debug.Log("Optical flow count: " + _opticalFlowCount);
						//Debug.Log("Nerf count: " + _nerfImageCount);
						if(_opticalFlowCount == FLOW_WINDOW_SIZE)
						{
							//Debug.Log("Captured flow color");
							_bCapturedColor = true;
						}
					}
					else
					{
						//Debug.Log("Captured color");
						_bCapturedColor = true;
					}
				}
			}
        }

        /// <summary>
        /// Create log information about the given texture.
        /// </summary>
        /// <param name="stringBuilder">The string builder to which to append the texture information.</param>
        /// <param name="textureName">The semantic name of the texture for logging purposes.</param>
        /// <param name="texture">The texture for which to log information.</param>
        void BuildTextureInfo(StringBuilder stringBuilder, string textureName, Texture2D texture)
        {
            stringBuilder.AppendLine($"texture : {textureName}");
            if (texture == null)
            {
                stringBuilder.AppendLine("   <null>");
            }
            else
            {
#if UNITY_IOS
#if UNITY_EDITOR
#else
				var sessionSubsystem = (ARKitSessionSubsystem)session.subsystem;
#endif
#endif
                stringBuilder.AppendLine($"   format : {texture.format}");
                stringBuilder.AppendLine($"   width  : {texture.width}");
                stringBuilder.AppendLine($"   height : {texture.height}");
                stringBuilder.AppendLine($"   mipmap : {texture.mipmapCount}");
				stringBuilder.AppendLine($"   orient : {Screen.orientation}");
				stringBuilder.AppendLine(UnityEngine.Time.deltaTime.ToString("F4"));
#if UNITY_IOS
#if UNITY_EDITOR

#else
				stringBuilder.AppendLine($"   ws : {sessionSubsystem.worldMappingStatus}");
#endif
#endif
				stringBuilder.AppendLine($"   ntr : {ARSession.notTrackingReason}");
				stringBuilder.AppendLine($"   state : {ARSession.state}");
				stringBuilder.AppendLine($"   exp: {_lastExposure}");
				stringBuilder.AppendLine($"   ct: {_lastColorTemp}");
				//stringBuilder.AppendLine($"   cc: {_lastCorrection.ToString()}");
				stringBuilder.AppendLine($"   li: {_lastIntensity}");
            }
        }

        /// <summary>
        /// Log the given text to the screen if the image info UI is set. Otherwise, log the string to debug.
        /// </summary>
        /// <param name="text">The text string to log.</param>
        void LogText(string text)
        {
            if (m_ImageInfo != null)
            {
                m_ImageInfo.text = text;
            }
            else
            {
                Debug.Log(text);
            }
        }
		
		public void StartCapture(bool bOn)
		{
			bool wasCapturing = _bIsCapturing;

			_bIsCapturing = bOn;

			if(wasCapturing)
			{
				_bDoneCapturing = true;
			}
			else
			{
				_currPath = Application.persistentDataPath + "/" + _currDate + "/" + _pressCount.ToString("D4");
				if(!Directory.Exists(_currPath))
				{
					Directory.CreateDirectory(_currPath);
				}

				_pressCount++;
				_opticalFlowCount = 0;
				_bDoneCapturing = false;
			}
		}
    }
//}
