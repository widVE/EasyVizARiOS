using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using TMPro;
using ZXing;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class QRCodeManager : MonoBehaviour
{
    public ARTrackedImageManager trackedImageManager;

    public Transform XROrigin;

    public Camera arCamera;
    public LineRenderer lineRenderer;
    public Button recordButton;
    public Button readButton;
    public TextMeshProUGUI positionText;
    public Quaternion qrCodeRotation;
    public Vector3 qrCodePosition = Vector3.zero;
    public bool qrCodeDetected = false;
   
    private Matrix4x4 qrCodeInverseTransform;
    //qr code part
    public string _locationID; //025f6d33-ee60-4bec-8286-184ce313340e
    private bool locFound = false;

    [SerializeField] private List<Vector3> positions = new List<Vector3>();
    [SerializeField] private string filePath;
    public bool isRecording = false;
    [SerializeField] private bool isReading = false;

    //new
    [SerializeField]
    private ARCameraManager cameraManager;

    [SerializeField]
    private string lastResult;

    private Texture2D cameraImageTexture = null;

    private IBarcodeReader barcodeReader = new BarcodeReader {
        AutoRotate = false,
        Options = new ZXing.Common.DecodingOptions {
            TryHarder = false
        }
    };

    string _headsetName = "iPhoneTestRoss"; // allow the user to set the name in the app.
	float _updateFrequency = 1f;
	float _lastTime;
	public string _headsetID;

    [SerializeField]
    SimpleCapture _capture;
  
    private void Start()
    {
        filePath = Path.Combine(Application.persistentDataPath, "positions.txt");
        recordButton.onClick.AddListener(OnRecordButtonClicked);
        readButton.onClick.AddListener(OnReadButtonClicked);

        if (cameraManager != null)
        {
            Debug.Log("camera manager is not null");
            cameraManager.frameReceived += OnCameraFrameReceived;
        }
        
        StartCoroutine(postPostionCoroutine());
    }

    private void OnEnable()
    {
        trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        if(!locFound) {
            cameraManager.frameReceived += OnCameraFrameReceived;
        } 
    }

    private void OnDisable()
    {
        trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
        if(!locFound) {
            cameraManager.frameReceived -= OnCameraFrameReceived;
        } 
    }

    private void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs) {
        // Debug.Log("locFund: " + locFound);
        if(!locFound && qrCodeDetected) 
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image)) {
                return;
            }

            var conversionParams = new XRCpuImage.ConversionParams {
                // Get the entire image.
                inputRect = new RectInt(0, 0, image.width, image.height),

                // Downsample by 2.
                outputDimensions = new Vector2Int(image.width, image.height),

                // Choose RGBA format.
                outputFormat = TextureFormat.RGBA32,

                // Flip across the vertical axis (mirror image).
                transformation = XRCpuImage.Transformation.MirrorY
            };

            // See how many bytes you need to store the final image.
            int size = image.GetConvertedDataSize(conversionParams);

            // Allocate a buffer to store the image.
            var buffer = new NativeArray<byte>(size, Allocator.Temp);

            // Extract the image data
            image.Convert(conversionParams, buffer);

            // The image was converted to RGBA32 format and written into the provided buffer
            // so you can dispose of the XRCpuImage. You must do this or it will leak resources.
            image.Dispose();

            // At this point, you can process the image, pass it to a computer vision algorithm, etc.
            // In this example, you apply it to a texture to visualize it.

            // You've got the data; let's put it into a texture so you can visualize it.
            if(cameraImageTexture == null)
            {
                cameraImageTexture = new Texture2D(
                    conversionParams.outputDimensions.x,
                    conversionParams.outputDimensions.y,
                    conversionParams.outputFormat,
                    false);
            }

            cameraImageTexture.LoadRawTextureData(buffer);
            cameraImageTexture.Apply();
            // Done with your temporary data, so you can dispose it.
            buffer.Dispose();
            // Detect and decode the barcode inside the bitmap
            Result result = barcodeReader.Decode(cameraImageTexture.GetPixels32(), cameraImageTexture.width, cameraImageTexture.height);
            
            //Destroy(cameraImageTexture);

            // Do something with the result
            if (result != null) 
            {
                locFound = true;
                string lastResult = result.Text;
                Debug.Log("lastResult: " + lastResult);

                //Uri uri = new Uri(lastResult);
                string path = lastResult;// uri.AbsolutePath;
                //Debug.Log(path);
                string[] segments = path.Split('/');
                
                if(segments.Length > 1)
                {
                    Debug.Log("0: " + segments[0]);
                    Debug.Log("1: " + segments[1]);
                    Debug.Log("2: " + segments[2]);
                    Debug.Log("3: " + segments[3]);
                    Debug.Log("4: " + segments[4]);

                    if (segments.Length > 2 && segments[3] == "locations")
                    {
                        _locationID = segments[4];
                        positionText.text = "locID: " + _locationID; 
                        //Debug.Log("_locationID: " + _locationID);
                        buffer.Dispose();
                        image.Dispose(); 
                        Debug.Log("Base URL: " + "http://"+segments[2]);
                        EasyVizARServer.Instance.SetBaseURL("http://" + segments[2] + "/");
                        CreateLocalHeadset();
                    }
                    else
                    {
                        Debug.Log("Location component not found!");
                    }
                }
            }
            else
            {
                //Debug.Log("Result null");
            }
        }
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.updated)
        {
            if(isRecording)
            {
                //Debug.Log(trackedImage.trackingState);
                if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking) 
                {  
                    //positionText.text = "qrCodePosition: " + trackedImage.transform.position;
                    if(!qrCodeDetected) 
                    {
                        //CreateLocalHeadset(); //new
                        qrCodePosition = trackedImage.transform.position;
                        qrCodeRotation = trackedImage.transform.rotation;
                        qrCodeDetected = true;
                    } 
                }
            }

            else if(isReading)
            {
                if (trackedImage.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking) 
                {
                    visualizePath(trackedImage.transform.position, trackedImage.transform.rotation);
                }
            }
        }
    }
    
    private void Update()
    {
        
    }

    IEnumerator postPostionCoroutine()
    {
        while(true)
        {
            if (qrCodeDetected && isRecording && locFound)
            {
                //Update function with rate limiting logic so that it is called approximately once per second.
                // Vector3 position = SavePosition(qrCodePosition, qrCodeRotation);

                Vector4 phonePos = Vector4.zero;
                //phone's current camera location...
                Vector3 p = arCamera.transform.position;

                phonePos.x = p.x;
                phonePos.y = p.y;
                phonePos.z = p.z;
                phonePos.w = 0f;

                // Vector3 relativePosition = qrCodeInverseTransform.MultiplyPoint3x4(phonePosition);

                //Matrix4x4 m = Matrix4x4.TRS(qrCodePosition, qrCodeRotation, Vector3.one);
                //Matrix4x4 mInv = m.inverse;
                
                //QR code position at the point at which the phone detects the code, relative to the phone's coordinate system...
                Vector4 qrPos = Vector4.zero;
                qrPos.x = qrCodePosition.x;
                qrPos.y = qrCodePosition.y;
                qrPos.z = qrCodePosition.z;
                qrPos.w = 1f;

                Vector4 phoneInQR = phonePos - qrPos;

                Quaternion qrCodeInv = Quaternion.Inverse(qrCodeRotation);
                
                Vector4 phoneInQRInv = qrCodeInv * phoneInQR;

                Vector4 relativePos = qrPos + phoneInQRInv;// + phonePos;

                Quaternion rotPhone = arCamera.transform.rotation;
                //Quaternion phoneInverse = Quaternion.Inverse(rotPhone);

                //Quaternion rotPhone = arCamera.transform.rotation * mInv.rotation;

                //Vector4 qrCodePositionWorld = mInv * v;//child.transform.position;

                //Vector4 relativePosition = qrCodePositionWorld - phonePosition;
                
                //Debug.Log("Updated Position Text: " + positionText.text);
                
                Vector3 temp = relativePos;
                //Vector3 temp = relativePosition;
                //temp.y = -relativePosition.z;
                //temp.z = relativePosition.y;
                
                //relativePosition = temp;

                Quaternion phoneInQRRot = qrCodeInv * rotPhone;

                PostPosition(relativePos, phoneInQRRot);

                positionText.text = "Phone Pos: " + phonePos + "\nQR Pos: " + qrPos + "\nPhone In QR: " + phoneInQR + "\nFinal Pos:" + relativePos;

                positions.Add(relativePos);

                SavePositionsToFile();
            
            }

            yield return new WaitForSeconds(_updateFrequency);
        }
    }
    
    private void SavePositionsToFile()
    {
        using (StreamWriter writer = new StreamWriter(filePath, false))
        {
            foreach (Vector3 position in positions)
            {
                string positionData = position.x + "," + position.y + "," + position.z;
                writer.WriteLine(positionData);
            }
        }
    }

    private void OnRecordButtonClicked()
    {
        isRecording = !isRecording;
        if(isRecording)
        {
            /*if(_capture != null)
            {
                _capture.StartCapture(true);
            }*/
            Debug.Log("QR Detected: " + qrCodeDetected);
            Debug.Log("Loc Found: " + locFound);
            Debug.Log("Writing to server...");
        }
        else
        {
            /*if(_capture != null)
            {
                _capture.StartCapture(false);
            }*/
            Debug.Log("QR Detected: " + qrCodeDetected);
            Debug.Log("Loc Found: " + locFound);
            Debug.Log("Not writing to server...");
        }
    }

    private void OnReadButtonClicked()
    {
        isReading = !isReading; 
        if(isReading)
        {
            Debug.Log("Reading from server...");
        }
        else
        {
            Debug.Log("Not reading from server...");
        }
    }

    private void visualizePath(Vector3 qrCodePositionNew, Quaternion qrCodeRotationNew)
    {
        lineRenderer.positionCount = 0;
        //lineRenderer.SetPositions(new Vector3[0]);
        positions.Clear();

        if (File.Exists(filePath))
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] positionData = line.Split(',');
                    if (positionData.Length == 3)
                    {
                        float x = float.Parse(positionData[0]);
                        float y = float.Parse(positionData[1]);
                        float z = float.Parse(positionData[2]);
                        Vector3 oldPosition = new Vector3(x, y, z);
                        positions.Add(oldPosition);
                    }
                }
            }
        }

        lineRenderer.positionCount = positions.Count;

        for (int i = 0; i < lineRenderer.positionCount; i++) 
        {
            // Vector3 worldSpacePoint = qrCodePosition + qrCodeRotation * point;

            Vector3 relativePosition = positions[i];

            // Use qrCodePositionWorld for visualization
            Vector3 originalPosition = relativePosition;//qrCodePositionWorld + relativePosition;
            
            lineRenderer.SetPosition(i, originalPosition);
            //Debug.Log("originalPosition: " + originalPosition);
        }
    }

    //new
    public void CreateLocalHeadset()
	{		// Either reload our existing headset from the server or create a new one.
		if (EasyVizARServer.Instance.TryGetHeadsetID(out string headsetId)) //Use EasyVizARServer.Instance.TryGetHeadsetID to check if your device is already registered with the server.
		{
			UnityEngine.Debug.Log("Reloading headset: " + headsetId);
			UnityEngine.Debug.Log("This is the local headset: " + headsetId);
			LoadHeadset(headsetId);
		} 
		else
		{
			UnityEngine.Debug.Log("Creating headset...");
			CreateHeadset();
		}
	}

	void LoadHeadset(string headsetId)
	{
		EasyVizARServer.Instance.Get("headsets/" + headsetId, EasyVizARServer.JSON_TYPE, LoadHeadsetCallback);
	}

	void LoadHeadsetCallback(string resultData)
	{
		if (resultData != "error")
		{
			// _isRegisteredWithServer = true;

			EasyVizAR.Headset h = JsonUtility.FromJson<EasyVizAR.Headset>(resultData);
			Vector3 newPos = Vector3.zero;

			//newPos.x = h.position.x;
			//newPos.y = h.position.y;
			//newPos.z = h.position.z;

			//transform.position = newPos;
			//transform.rotation = new Quaternion(h.orientation.x, h.orientation.y, h.orientation.z, h.orientation.w);

			// We should load the name and color information from the server, but not the location ID, which may be out of date.
			// Instead, since we probably just scanned a QR code, we should inform the server of our new location by
			// sending a check-in.
			_headsetID = h.id;
			_headsetName = h.name;

			// Color newColor;
			// if (ColorUtility.TryParseHtmlString(h.color, out newColor))
			// 	_color = newColor;

			UnityEngine.Debug.Log("Successfully connected headset LoadHeadsetCallback: " + h.name);
            Debug.Log("in load headset locID:" + _locationID);
			CreateCheckIn(h.id, _locationID);
		}
		else
		{
			// If loading fails, make a new headset.
			CreateHeadset();
		}
	}

	public void CreateCheckIn(string headsetId, string locationId)
	{
		var checkIn = new EasyVizAR.NewCheckIn();
		checkIn.location_id = locationId;

		EasyVizARServer.Instance.Post($"/headsets/{headsetId}/check-ins", EasyVizARServer.JSON_TYPE, JsonUtility.ToJson(checkIn), delegate (string result)
		{
			// Not much to do after check-in was created.
		});
	}



	void CreateHeadset() //register a new headset
	{
		EasyVizAR.Headset h = new EasyVizAR.Headset();
		h.position = new EasyVizAR.Position();
		h.position.x = arCamera.transform.position.x;
		h.position.y = arCamera.transform.position.y;
		h.position.z = arCamera.transform.position.z;
		h.orientation = new EasyVizAR.Orientation();
		h.orientation.x = arCamera.transform.rotation[0];
		h.orientation.y = arCamera.transform.rotation[1];
		h.orientation.z = arCamera.transform.rotation[2];
		h.orientation.w = arCamera.transform.rotation[3];
		
		h.name = _headsetName;
		h.location_id = _locationID;
        UnityEngine.Debug.Log("CreateHeadset: " + _locationID);
		EasyVizARServer.Instance.Post("headsets", EasyVizARServer.JSON_TYPE, JsonUtility.ToJson(h), CreateCallback);
	}


	void CreateCallback(string resultData)
	{
		if(resultData != "error")
		{
			EasyVizAR.RegisteredHeadset h = JsonUtility.FromJson<EasyVizAR.RegisteredHeadset>(resultData);
			Vector3 newPos = Vector3.zero;

			//newPos.x = h.position.x;
			//newPos.y = h.position.y;
			//newPos.z = h.position.z;	
			
			//transform.position = newPos;
			//transform.rotation = new Quaternion(h.orientation.x, h.orientation.y, h.orientation.z, h.orientation.w);
			
			_headsetID = h.id;
			_headsetName = h.name;
			_locationID = h.location_id;
            UnityEngine.Debug.Log("create call back locID: " + _locationID);
            //need
			EasyVizARServer.Instance.SaveRegistration(h.id, h.token); //u can see in the callback after registering a headset, we save the headset ID and token for future use here
            //instance
			UnityEngine.Debug.Log("Successfully connected headset LoadHeadsetCallback: " + h.name);
		}
		else
		{
			UnityEngine.Debug.Log("Received an error when creating headset");
		}
	}


	void PostPositionCallback(string resultData)
	{
		//Debug.Log(resultData);
		
		if(resultData != "error")
		{
			
		}
		else
		{
			
		}
	}


	// We use a patch request with a HeadsetPositionUpdate structure. Periodically update the headset position and orientation on the server.
	void PostPosition(Vector3 qrCodePosition, Quaternion qrCodeRotation) 
    {
        if(_capture != null)
        {
            //Debug.Log("Capturing");
            _capture._currentPosition = qrCodePosition;
            _capture._currentRotation = qrCodeRotation;
            _capture.StartCapture(true);
        }

		EasyVizAR.HeadsetPositionUpdate h = new EasyVizAR.HeadsetPositionUpdate();
		h.position = new EasyVizAR.Position();
		// h.position.x = arCamera.transform.position.x;
		// h.position.y = arCamera.transform.position.y;
		// h.position.z = arCamera.transform.position.z;
		h.orientation = new EasyVizAR.Orientation();
		// h.orientation.x = arCamera.transform.rotation[0];
		// h.orientation.y = arCamera.transform.rotation[1];
		// h.orientation.z = arCamera.transform.rotation[2];
		// h.orientation.w = arCamera.transform.rotation[3];
        // h.position = qrCodePosition;
		h.position.x = qrCodePosition.x;
		h.position.y = qrCodePosition.y;
		h.position.z = qrCodePosition.z;
		// h.orientation = qrCodeRotation;
		h.orientation.x = qrCodeRotation[0];
		h.orientation.y = qrCodeRotation[1];
		h.orientation.z = qrCodeRotation[2];
		h.orientation.w = qrCodeRotation[3];
		h.location_id = _locationID;
		
		EasyVizARServer.Instance.Patch("headsets/"+_headsetID, EasyVizARServer.JSON_TYPE, JsonUtility.ToJson(h), PostPositionCallback);
	}
}