/************************************************************************************

Copyright (c) Facebook Technologies, LLC and its affiliates. All rights reserved.  

See SampleFramework license.txt for license terms.  Unless required by applicable law 
or agreed to in writing, the sample code is provided “AS IS” WITHOUT WARRANTIES OR 
CONDITIONS OF ANY KIND, either express or implied.  See the license for specific 
language governing permissions and limitations under the license.

************************************************************************************/

using UnityEngine;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class VRLaserPointer : MonoBehaviour
{
    public enum LaserBeamBehavior
    {
        On,        // laser beam always on
        Off,        // laser beam always off
        OnWhenHitTarget,  // laser beam only activates when hit valid target
    }

    public GameObject cursorVisual;
    public float LineBackDistance = 0.09f;
    public float DefaultLength = 10.0f;

    private LaserBeamBehavior _laserBeamBehavior = LaserBeamBehavior.Off;

    public LaserBeamBehavior laserBeamBehavior
    {
        set
        {
            _laserBeamBehavior = value;
            if (laserBeamBehavior == LaserBeamBehavior.Off || laserBeamBehavior == LaserBeamBehavior.OnWhenHitTarget)
            {
                lineRenderer.enabled = false;
            }
            else
            {
                lineRenderer.enabled = true;
            }
        }
        get
        {
            return _laserBeamBehavior;
        }
    }
    // If present, the start and end point
    // will be considered as local points from
    // these transforms
    private Transform _startTransform;
    private Transform _endTransform;

    private Vector3 _startPointLocal;
    private Vector3 _endPointLocal;
    private Vector3 _cursorPosLocal;
    private bool _hitTarget;
    private XRLineRenderer lineRenderer;
    private bool _useCursor;

    private void Awake()
    {
        lineRenderer = GetComponent<XRLineRenderer>();
    }

    private void Start()
    {
        if (cursorVisual) cursorVisual.SetActive(false);
    }

    public void SetCursorStartDest(Vector3 start, Vector3 dest, bool useCursor=true)
    {
        _startTransform = null;
        _endTransform = null;
        _startPointLocal = start;
        _endPointLocal = dest;
        _hitTarget = true;
        _useCursor = useCursor;
    }
    /// <summary>
    /// Used when we know where we want the line to be, but
    /// we also want to re-evaluate the positions at the end of the frame
    /// </summary>
    /// <param name="startTransform"></param>
    /// <param name="destPosLocal"></param>
    /// <param name="destTransform"></param>
    /// <param name="useCursor"></param>
    public void SetCursorStartDest(Transform startTransform, Vector3 destPosLocal, Transform destTransform, bool useCursor=true)
    {
        _startTransform = startTransform;
        _endTransform = destTransform;
        _startPointLocal = Vector3.zero;
        _endPointLocal = destPosLocal;
        //_endPointLocal = destPosLocal - (Vector3.forward) * LineBackDistance;
        _cursorPosLocal = destPosLocal;
        _hitTarget = true;
        _useCursor = useCursor;
    }
    public void SetCursorRay(Transform t, bool useCursor=true)
    {
        _startTransform = t;
        _endTransform = t;
        _startPointLocal = Vector3.zero;
        //_endPointLocal = t.forward * DefaultLength;
        _endPointLocal = Vector3.forward * DefaultLength;
        _hitTarget = false;
        _useCursor = useCursor;
    }

    //TODO this should not be late update, we should instead listen to an event from ControllerAbstraction
    private void LateUpdate()
    {
        Vector3 startPt;
        if (_startTransform != null)
            startPt = _startTransform.TransformPoint(_startPointLocal);
        else
            startPt = _startPointLocal;

        Vector3 endPt = _endTransform != null ? _endTransform.TransformPoint(_endPointLocal) : _endPointLocal;
        if (_hitTarget)
            endPt = (endPt - startPt) * (1 - LineBackDistance) + startPt;
        UpdateLaserBeam(startPt, endPt);

        if (cursorVisual == null)
            return;

        if(_hitTarget && _useCursor)
        {
            Vector3 cursorPos = _endTransform != null ? _endTransform.TransformPoint(_cursorPosLocal) : _cursorPosLocal;
            cursorVisual.transform.position = cursorPos;
            cursorVisual.SetActive(true);
        }
        else
        {
            cursorVisual.SetActive(false);
        }
    }

    // make laser beam a behavior with a prop that enables or disables
    private void UpdateLaserBeam(Vector3 start, Vector3 end)
    {
        if (laserBeamBehavior == LaserBeamBehavior.Off)
        {
            return;
        }
        else if (laserBeamBehavior == LaserBeamBehavior.On)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
        else if (laserBeamBehavior == LaserBeamBehavior.OnWhenHitTarget)
        {
            if (_hitTarget)
            {
                if (!lineRenderer.enabled)
                {
                    lineRenderer.enabled = true;
                    lineRenderer.SetPosition(0, start);
                    lineRenderer.SetPosition(1, end);
                }
            }
            else
            {
                if (lineRenderer.enabled)
                {
                    lineRenderer.enabled = false;
                }
            }
        }
    }

    void OnDisable()
    {
        if (cursorVisual) cursorVisual.SetActive(false);
    }
}
