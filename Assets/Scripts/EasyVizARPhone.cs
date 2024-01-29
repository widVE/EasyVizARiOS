// //vizar://easyvizar.wings.cs.wisc.edu/locations/025f6d33-ee60-4bec-8286-184ce313340e
// //025f6d33-ee60-4bec-8286-184ce313340e
// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.IO;
// using System.Security.Cryptography;
// using UnityEngine;


// public class EasyVizARPhone : MonoBehaviour
// {
// 	public Camera arCamera;
// 	string _headsetName = "Jinlang's Phone"; // allow the user to set the name in the app.
// 	float _updateFrequency = 3;
// 	float _lastTime;
// 	public string _headsetID;

// 	// public string _locationID = "025f6d33-ee60-4bec-8286-184ce313340e";// will change this back to just string w/o public
// 	string _locationID;	
// 	// public string LocationID
// 	// {
// 	// 	get { return _locationID; }
// 	// 	set { _locationID = value; }
// 	// }

//     // Start is called before the first frame update
//     void Start()
//     {
		
// 		//new
//         QRCodeManager scriptAInstance = FindObjectOfType<QRCodeManager>();
//         if (scriptAInstance != null)
//         {
//              _locationID = scriptAInstance._locationID;
//         }

// 		// currentTarget.type = "none";

// 		_lastTime = UnityEngine.Time.time;
//         // line = GameObject.Find("Main Camera").GetComponent<LineRenderer>();

//         //Debug.Log("In start");
//         //CreateHeadset();
//         //RegisterHeadset();
// 		CreateLocalHeadset();
//     }
//     // 
//     // check if your device is already registered with the server.
//     // public void CreateLocalHeadset(string headsetName, string location)
//     public void CreateLocalHeadset()
// 	{
// 		// _isLocal = true;
// 		// _headsetName = headsetName;
// 		// _locationID = location;
		
// 		// if(postChanges)
// 		// {
// 			// _realTimeChanges = true;

// 			// DistanceCalculation d_s = this.GetComponent<DistanceCalculation>();

// 			// Either reload our existing headset from the server or create a new one.
// 		if (EasyVizARServer.Instance.TryGetHeadsetID(out string headsetId)) //Use EasyVizARServer.Instance.TryGetHeadsetID to check if your device is already registered with the server.
// 		{
// 			UnityEngine.Debug.Log("Reloading headset: " + headsetId);
// 			UnityEngine.Debug.Log("This is the local headset: " + headsetId);
// 			// local_headset_id = headsetId;
// 			// is_local= true;
// 			// d_s.is_local = true;
// 			LoadHeadset(headsetId);
// 		} 
// 		else
// 		{
// 			UnityEngine.Debug.Log("Creating headset...");
// 			CreateHeadset();
// 		}
// 		// }
// 	}

// 	void LoadHeadset(string headsetId)
// 	{
// 		EasyVizARServer.Instance.Get("headsets/" + headsetId, EasyVizARServer.JSON_TYPE, LoadHeadsetCallback);
// 	}

// 	void LoadHeadsetCallback(string resultData)
// 	{
// 		if (resultData != "error")
// 		{
// 			// _isRegisteredWithServer = true;

// 			EasyVizAR.Headset h = JsonUtility.FromJson<EasyVizAR.Headset>(resultData);
// 			Vector3 newPos = Vector3.zero;

// 			newPos.x = h.position.x;
// 			newPos.y = h.position.y;
// 			newPos.z = h.position.z;

// 			transform.position = newPos;
// 			transform.rotation = new Quaternion(h.orientation.x, h.orientation.y, h.orientation.z, h.orientation.w);

// 			// We should load the name and color information from the server, but not the location ID, which may be out of date.
// 			// Instead, since we probably just scanned a QR code, we should inform the server of our new location by
// 			// sending a check-in.
// 			_headsetID = h.id;
// 			_headsetName = h.name;

// 			// Color newColor;
// 			// if (ColorUtility.TryParseHtmlString(h.color, out newColor))
// 			// 	_color = newColor;

// 			UnityEngine.Debug.Log("Successfully connected headset: " + h.name);

// 			CreateCheckIn(h.id, _locationID);
// 		}
// 		else
// 		{
// 			// If loading fails, make a new headset.
// 			CreateHeadset();
// 		}
// 	}

// 	public void CreateCheckIn(string headsetId, string locationId)
// 	{
// 		var checkIn = new EasyVizAR.NewCheckIn();
// 		checkIn.location_id = locationId;

// 		EasyVizARServer.Instance.Post($"/headsets/{headsetId}/check-ins", EasyVizARServer.JSON_TYPE, JsonUtility.ToJson(checkIn), delegate (string result)
// 		{
// 			// Not much to do after check-in was created.
// 		});
// 	}



// 	void CreateHeadset() //register a new headset
// 	{
// 		EasyVizAR.Headset h = new EasyVizAR.Headset();
// 		h.position = new EasyVizAR.Position();
// 		h.position.x = arCamera.transform.position.x;
// 		h.position.y = arCamera.transform.position.y;
// 		h.position.z = arCamera.transform.position.z;
// 		h.orientation = new EasyVizAR.Orientation();
// 		h.orientation.x = arCamera.transform.rotation[0];
// 		h.orientation.y = arCamera.transform.rotation[1];
// 		h.orientation.z = arCamera.transform.rotation[2];
// 		h.orientation.w = arCamera.transform.rotation[3];
		
// 		h.name = _headsetName;
// 		h.location_id = _locationID;
		
// 		EasyVizARServer.Instance.Post("headsets", EasyVizARServer.JSON_TYPE, JsonUtility.ToJson(h), CreateCallback);
// 	}


// 	void CreateCallback(string resultData)
// 	{
// 		if(resultData != "error")
// 		{
// 			// _isRegisteredWithServer = true;
			
// 			EasyVizAR.RegisteredHeadset h = JsonUtility.FromJson<EasyVizAR.RegisteredHeadset>(resultData);
// 			Vector3 newPos = Vector3.zero;

// 			newPos.x = h.position.x;
// 			newPos.y = h.position.y;
// 			newPos.z = h.position.z;	
			
// 			transform.position = newPos;
// 			transform.rotation = new Quaternion(h.orientation.x, h.orientation.y, h.orientation.z, h.orientation.w);
			
// 			_headsetID = h.id;
// 			_headsetName = h.name;
// 			_locationID = h.location_id;

// 			// Color newColor;
// 			// if (ColorUtility.TryParseHtmlString(h.color, out newColor))
// 			// 	_color = newColor;

//             //need
// 			EasyVizARServer.Instance.SaveRegistration(h.id, h.token); //u can see in the callback after registering a headset, we save the headset ID and token for future use here
//             //instance
// 			UnityEngine.Debug.Log("Successfully connected headset: " + h.name);
// 		}
// 		else
// 		{
// 			UnityEngine.Debug.Log("Received an error when creating headset");
// 		}
// 	}


// 	void PostPositionCallback(string resultData)
// 	{
// 		//Debug.Log(resultData);
		
// 		if(resultData != "error")
// 		{
			
// 		}
// 		else
// 		{
			
// 		}
// 	}


// 	// We use a patch request with a HeadsetPositionUpdate structure. Periodically update the headset position and orientation on the server.
// 	void PostPosition() 
//     {
// 		EasyVizAR.HeadsetPositionUpdate h = new EasyVizAR.HeadsetPositionUpdate();
// 		h.position = new EasyVizAR.Position();
// 		h.position.x = arCamera.transform.position.x;
// 		h.position.y = arCamera.transform.position.y;
// 		h.position.z = arCamera.transform.position.z;
// 		h.orientation = new EasyVizAR.Orientation();
// 		h.orientation.x = arCamera.transform.rotation[0];
// 		h.orientation.y = arCamera.transform.rotation[1];
// 		h.orientation.z = arCamera.transform.rotation[2];
// 		h.orientation.w = arCamera.transform.rotation[3];
// 		h.location_id = _locationID;
		
// 		EasyVizARServer.Instance.Patch("headsets/"+_headsetID, EasyVizARServer.JSON_TYPE, JsonUtility.ToJson(h), PostPositionCallback);
// 	}



//     // Update is called once per frame
//     void Update() 
//     {
// 		// if(_isLocal)
// 		// {
// 		// 	// if(_mainCamera && _postPositionChanges)
// 		// 	if(_mainCamera )
// 		// 	{
// 		// 		transform.position = _mainCamera.transform.position;
// 		// 		transform.rotation = _mainCamera.transform.rotation;
// 		// 	}
			
// 		// 	float t = UnityEngine.Time.time;
// 		// 	if(t - _lastTime > _updateFrequency) //Update function with rate limiting logic so that it is called approximately once per second.
// 		// 	{
// 		// 		// if(_isRegisteredWithServer && _postPositionChanges)
// 		// 		if(_isRegisteredWithServer)
// 		// 		{
// 		// 			PostPosition();
// 		// 		}
// 		// 		_lastTime = t;
				
// 		// 		if(_realTimeChanges && _showPositionChanges)
// 		// 		{
// 		// 			GetPastPositions();
// 		// 		}
// 		// 	}
// 		// }

// 		float t = UnityEngine.Time.time;

// 		if(t - _lastTime > _updateFrequency) //Update function with rate limiting logic so that it is called approximately once per second.
// 		{
// 			QRCodeManager scriptAInstance = FindObjectOfType<QRCodeManager>();
// 			if (scriptAInstance != null)
// 			{
// 				if(scriptAInstance.isRecording && scriptAInstance.qrCodeDetected) 
// 				{
// 					PostPosition();
// 				}
// 			}
// 		}
//     }
// }