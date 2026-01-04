using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LjomaAssets.FormManagement.Inputs
{

    public class ColorPicker : FormInput, IPointerDownHandler
    {
        private Image _colorWheelImage;
        
        private void Awake()
        {
            _colorWheelImage = GetComponent<Image>();
        }
        
        protected override void SetupFormInput()
        {
            throw new System.NotImplementedException();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            RectTransform imageRect = _colorWheelImage.rectTransform;

            // Convert the screen position to local position within the image rect
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(imageRect, eventData.position,
                eventData.pressEventCamera, out localPos);

            // Normalize the local position to get UV coordinates
            Vector2 uv = new Vector2(
                Mathf.InverseLerp(imageRect.rect.x, imageRect.rect.xMax, localPos.x),
                Mathf.InverseLerp(imageRect.rect.y, imageRect.rect.yMax, localPos.y)
            );

            // Get the color from the texture at the UV coordinates
            Color pickedColor = _colorWheelImage.sprite.texture.GetPixelBilinear(uv.x, uv.y); 
            Debug.Log("Picked Color: " + pickedColor);
        }
    }
}