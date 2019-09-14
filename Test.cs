using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TriangleNet;
using TriangleNet.Meshing;
using TriangleNet.Meshing.Algorithm;
using TriangleNet.Geometry;
using TriangleNet.Topology;
//using TriangleNet.Adjacency;

public class Test : MonoBehaviour 
{
	//estalishing the vectors that contain the vertexes, triangles, and colors for the mesh
	int n = 100;
	Vector3 [] points;
	public enum PointState {FIXED, FREE};
	public PointState[] pointState;
	Color [] colors;
	Vector3 [] normals;
	int [] triangles;

	void Start () 
	{
		//Creates other randomnly generated mesh with random Vector3 points
		points = new Vector3[n];
		pointState = new PointState[n];
		for (int i = 0; i < n; i++) 
		{
			points [i] = Random.insideUnitSphere * 5;
			//To avoid massive Y values we have limited it to 10%
			points [i].y *= 0.1f;
			points [i].y +=1.0f;
			pointState [i] = PointState.FIXED;
		}
		//Iniates the CreateMesh scripts
		CreateMesh ();
	}
	//Adds Centroids to the middle of the triangles and adds them to other separate array of vector3s
	void addCentroids() 
	{
		List<Vector3> newPoints = new List<Vector3> ();
		for (int i = 0; i < triangles.Length; i+=3) 
		{
			Vector3 other = points [triangles [i]];
			Vector3 b = points [triangles [i+1]];
			Vector3 c = points [triangles [i+2]];
			Vector3 centroid = (other + b + c) / 3;
			//the y value for the centroids is initially going to be an avg of the vertices of other triangle
			//centroid.y = 0;
			newPoints.Add (centroid);
		}
		//Creates new variable newLength to hold the variable length of the arrays and more centroids are added so that propper memory can be allocated
		int newLength = points.Length + newPoints.Count;
		Vector3 [] temp = new Vector3[newLength];
		PointState [] tempState = new PointState[newLength];
		for (int i = 0; i < points.Length; i++) 
		{
			temp [i] = points [i];
			tempState [i] = pointState [i];
		}
		for (int i = 0; i < newPoints.Count; i++) 
		{
			temp [i + points.Length] = newPoints [i];
			tempState [i + points.Length] = PointState.FREE;
		}
		points = temp;
		pointState = tempState;
	}

	void CreateMesh () 
	{
		colors = new Color[points.Length];
		normals = new Vector3[points.Length];

		for (int i = 0; i < points.Length; i++) 
		{
			float cscale = (points [i].y - 0.9f) / 0.2f;
			colors [i] = new Color (cscale, 0, 1 - cscale);
			normals [i] = new Vector3 (0, 1, 0);
		}
		GenericMesher gm = new GenericMesher ();
		List<Vertex> vertices = new List<Vertex> ();
		for (int i = 0; i < points.Length; i++) 
		{
			Vertex v = new Vertex (points [i].x, points [i].z);
			vertices.Add (v);
		}

		//Dwyer dw = new Dwyer ();
		//Configuration con = new Configuration ();
		//dw.Triangulate (vertices, con);

		IMesh imesh = gm.Triangulate (vertices);
		ICollection<Triangle> tri = imesh.Triangles;
		int ntri = tri.Count;
		triangles = new int[3 * ntri];
		int ctri = 0;
		foreach (Triangle triangle in tri) 
		{
			triangles[ctri] = triangle.GetVertexID (0);
			triangles[ctri+1] = triangle.GetVertexID (1);
			triangles[ctri+2] = triangle.GetVertexID (2);
			ctri += 3;
		}

		UnityEngine.Mesh mesh = GetComponent<MeshFilter> ().mesh;
		mesh.vertices = points;
		mesh.triangles = triangles;
		mesh.normals = normals;
		mesh.colors = colors;
		mesh.RecalculateNormals ();
		mesh.RecalculateBounds ();

	}

	bool updateCounts=true;
	// Update is called once per frame
	void Update () 
	{
		if (Input.GetButtonDown ("Fire1")) 
		{
			addCentroids ();
			CreateMesh ();
			updateCounts = true;
		}

		if (Input.GetButtonDown ("Fire2")) {
			Smooth (1,updateCounts,true);
			Smooth (1500,updateCounts,false);
			for (int i = 0; i < points.Length; i++) 
			{
				float cscale = (points [i].y - 0.9f) / 0.2f;
				colors [i] = new Color (cscale, 0, 1 - cscale);
			}
			UnityEngine.Mesh mesh = GetComponent<MeshFilter> ().mesh;
			mesh.vertices = points;
			mesh.colors = colors;

			updateCounts = false;

		}
	}

	List<int>[] nearby ;
	void Smooth(int ntrials,bool newCounts,bool smoothFixed) {
		float[] storage = new float[points.Length];
		if(newCounts) nearby = new List<int>[points.Length];
		for(int iter = 0;iter<ntrials;iter++) {
			for (int i = 0; i < points.Length; i++) {
				if(iter==0&&newCounts) 
					nearby[i] = getNearby (i,5.0f*Mathf.Sqrt(1.0f/points.Length));
				if (pointState [i] == PointState.FREE||smoothFixed) {
					int count = nearby[i].Count;
					if (count > 0) {
						float sum = 0.0f;
						for (int j = 0; j < nearby[i].Count; j++) {
							sum += points [nearby[i][j]].y;
						}
						sum /= (float)count;
						storage[i] = sum;
					}
				}
			}
			for (int i = 0; i < points.Length; i++) {
				if (pointState [i] == PointState.FREE||smoothFixed) {
					points [i].y = storage [i];
				}
			}
		}
	}

	public List<int> getNearby(int base1, float radius)
	{
		List<int> returnvalues;
		returnvalues = new List<int> ();

		bool foundNeighbors = false;
		int iter = 0;
		int itmax=10;
		while(!foundNeighbors&& iter++<itmax) 
		{
			returnvalues = new List<int> (); //reiterates the list
			float rsquare = radius * radius ; //Creating variable rsquare will eliminate the need to run the multiplication each time through the for loop--> better speeds
			//To come up with an accurate average Y value, we need to test every vertex with every other vertex and make sure that the distance fits within a given radius.
			//But, if there are not enough data points, the approximation for the new Y-value of the centroid will not be accurate. j
			//Therefore, w.e need to create a system that will increase both the original vertex and each other vertex by one, making sure to never overlap.
			for (int other = 0; other < points.Length; other++) //increasing the comparison vertex number in the array by 1
			{
				if (other != base1) // We do not want to subtract the original point by itself bc that will just yield 0 and sway the average
				{
					Vector3 delta = points[other] - points[base1];
					if(delta.sqrMagnitude <= rsquare) //Checking to see if the vertex is within the radius
					{
						returnvalues.Add(other); //Adds the ="Other" Vector3 to the list returnValues
					}
				}
			}
			if (returnvalues.Count < 3 ) {
				radius *= 2.0f; //exponentially increases the radius
			} else if (returnvalues.Count > 10 ) {
				radius *= 0.4f; //exponentially increases the radius
			} else 
				
			{
				foundNeighbors = true; //ends while loop
			}
		}
		if (!foundNeighbors) {

		}
		return returnvalues;
	}
	// Below code is under construction
	//public float AverageYValues(int avgvalue)
	//{
	//	for(int i = 0; i < returnvalues.Count; i++)
	//	{
	//		avgvalue += returnvalues[i.y];
	//	}
	//	avgvalue /= (float)returnvalues.Count;
	//}
}