using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorPlanMap : MonoBehaviour
{
    public bool highlightAgent = false;
    public bool highlightTarget = false;

    void Start()
    {
        HighlightObject(GameObject.Find("Sofa0"), false);
    }
    public void HighlightObject(GameObject m_object, bool isTarget)
    {
        LineRenderer lr = m_object.AddComponent<LineRenderer>() as LineRenderer;
        lr.startColor = isTarget? Color.green:Color.blue;
        lr.endColor = isTarget? Color.green:Color.blue;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 31;

        // Check which upAxis vector is in line with world y axis
        int upAxis = 0;
        if(Vector3.Dot(m_object.transform.right, Vector3.up)==1f) {upAxis=0;}
        if(Vector3.Dot(m_object.transform.up, Vector3.up)==1f) {upAxis=1;}
        if(Vector3.Dot(m_object.transform.forward, Vector3.up)==1f) {upAxis=2;}

        // Get the bounds of the gameobject
        Bounds bounds;
        if(m_object.GetComponent<Renderer>()==null)
        {
            bounds = m_object.GetComponent<Collider>().bounds;
        }
        else
        {
            bounds = m_object.GetComponent<Renderer>().bounds;
        }

        // Create points for the object and set to line renderer
        lr.SetPositions(CreatePoints(upAxis, bounds));
    }

    public void DeHightlightObject(GameObject m_object)
    {
        Destroy(m_object.GetComponent<LineRenderer>());
    }

    // Create points for line renderer
    private Vector3[] CreatePoints(int upAxis, Bounds bounds)
    {
        Vector3[] points = new Vector3[31];
        float a;
        float b;

        float angle = 0f;

        for (int i = 0; i < 31; i++)
        {
            switch(upAxis)
            {
                case 0: // upAxis along world x axis
                    a = Mathf.Sin (Mathf.Deg2Rad * angle) * bounds.extents.y;
                    b = Mathf.Cos (Mathf.Deg2Rad * angle) * bounds.extents.z;
                    points[i] = new Vector3(bounds.center.x, a, b);
                    break;
                case 1: // upAxis along world y axis
                    a = Mathf.Sin (Mathf.Deg2Rad * angle) * bounds.extents.x;
                    b = Mathf.Cos (Mathf.Deg2Rad * angle) * bounds.extents.z;
                    points[i] = new Vector3(a, bounds.center.y, b);
                    break;
                case 2: // upAxis along world z axis
                    a = Mathf.Sin (Mathf.Deg2Rad * angle) * bounds.extents.x;
                    b = Mathf.Cos (Mathf.Deg2Rad * angle) * bounds.extents.y;
                    points[i] = new Vector3(a, b, bounds.center.z);
                    break;
            }

            angle += 12f;
        }

        return points;
    }
}
