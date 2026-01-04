using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CursorHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Texture2D customCursor; // Drag your cursor texture here in the Inspector
    public Texture2D defaultCursor; // This can be null if you want to use the default system cursor
    public Vector2 hotSpot = Vector2.zero; // Cursor hotspot (adjust as needed)
    public CursorMode cursorMode = CursorMode.Auto;

    private void Start()
    {
        hotSpot = new Vector2(customCursor.width / 2.0f, customCursor.height / 2.0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Cursor.SetCursor(customCursor, hotSpot, cursorMode);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Cursor.SetCursor(defaultCursor, Vector2.zero, cursorMode);
    }

    public void ResetCursors()
    {
        Cursor.SetCursor(defaultCursor, Vector2.zero, cursorMode);
    }
}
