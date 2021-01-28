using UnityEngine;
using TMPro;
namespace Map
{
    public class MapGUI : MonoBehaviour
    {
        [SerializeField]
        private MapManager map = null;
        [SerializeField]
        private TMP_Text infoText = null;

        private bool infoUpdated = false;
        private string infoString = string.Empty;
        private float updateRate = 0.08f;
        private float lastUpdate = 0f;
        private int currentHoverID = 0;
        private int currentOriginID = 0;
        private int currentGoalID = 0;

        private void OnEnable()
        {
            if (map == null || infoText == null)
                gameObject.SetActive(false);
        }
        private void Update()
        {
            if (Time.timeSinceLevelLoad < lastUpdate) return;
            if (map.CurrentHovered == null)
            {
                if(currentHoverID > -1)
                {
                    infoUpdated = true;
                    currentHoverID = -1;
                }
            }
            else
            {
                if(currentHoverID != map.CurrentHovered.GetInstanceID())
                {
                    infoUpdated = true;
                    currentHoverID = map.CurrentHovered.GetInstanceID();
                }
            }
            if(map.CurrentOrigin == null)
            {
                if(currentOriginID > -1)
                {
                    infoUpdated = true;
                    currentOriginID = -1;
                }
            }
            else
            {
                if(currentOriginID != map.CurrentOrigin.GetInstanceID())
                {
                    infoUpdated = true;
                    currentOriginID = map.CurrentOrigin.GetInstanceID();
                }
            }
            if(map.CurrentGoal == null)
            {
                if(currentGoalID > -1)
                {
                    infoUpdated = true;
                    currentGoalID = -1;
                }
            }
            else
            {
                if(currentGoalID != map.CurrentGoal.GetInstanceID())
                {
                    infoUpdated = true;
                    currentGoalID = map.CurrentGoal.GetInstanceID();
                }
            }
            if(infoUpdated)
            {
                infoString = string.Format("Selected: \t{0}\nOrigin: \t{1}\nGoal: \t\t{2}",
                    map.CurrentHovered == null ? "NULL" : map.CurrentHovered.name,
                    map.CurrentOrigin == null ? "NULL" : map.CurrentOrigin.name,
                    map.CurrentGoal == null ? "NULL" : map.CurrentGoal.name);
                infoText.text = infoString;
                infoText.SetAllDirty();
            }
            lastUpdate = Time.timeSinceLevelLoad + updateRate;
        }
    }
}