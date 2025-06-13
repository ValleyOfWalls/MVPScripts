using UnityEngine;
using System.Diagnostics;

/// <summary>
/// Monitors when a UI panel gets disabled and logs detailed information about what caused it
/// </summary>
public class PanelDisableMonitor : MonoBehaviour
{
    private string panelName = "Unknown Panel";
    
    public void SetPanelName(string name)
    {
        panelName = name;
        UnityEngine.Debug.Log($"[PanelDisableMonitor] Monitoring panel: {panelName}");
    }
    
    private void OnEnable()
    {
        UnityEngine.Debug.Log($"[PanelDisableMonitor] {panelName} ENABLED at {Time.time:F3}");
        LogStackTrace("ENABLE");
    }
    
    private void OnDisable()
    {
        UnityEngine.Debug.Log($"[PanelDisableMonitor] {panelName} DISABLED at {Time.time:F3}");
        LogStackTrace("DISABLE");
    }
    
    private void LogStackTrace(string action)
    {
        StackTrace stackTrace = new StackTrace(true);
        UnityEngine.Debug.Log($"[PanelDisableMonitor] {panelName} {action} STACK TRACE:");
        
        // Log the first few relevant stack frames
        for (int i = 0; i < UnityEngine.Mathf.Min(10, stackTrace.FrameCount); i++)
        {
            StackFrame frame = stackTrace.GetFrame(i);
            if (frame.GetMethod() != null)
            {
                string methodName = frame.GetMethod().DeclaringType?.Name + "." + frame.GetMethod().Name;
                string fileName = frame.GetFileName();
                int lineNumber = frame.GetFileLineNumber();
                
                // Skip our own monitor methods
                if (methodName.Contains("PanelDisableMonitor"))
                    continue;
                    
                if (!string.IsNullOrEmpty(fileName))
                {
                    UnityEngine.Debug.Log($"[PanelDisableMonitor]   Frame {i}: {methodName} at {System.IO.Path.GetFileName(fileName)}:{lineNumber}");
                }
                else
                {
                    UnityEngine.Debug.Log($"[PanelDisableMonitor]   Frame {i}: {methodName}");
                }
            }
        }
    }
} 