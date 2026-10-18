using UnityEngine;
using UnityEngine.UI;

public class TalismanDevPanel : MonoBehaviour
{
    [SerializeField] private TalismanDrawingSystem drawing;
    [SerializeField] private Text dataText;

    private void Update()
    {
        if (drawing != null && dataText != null)
        {
            dataText.text = drawing.GetDebugText()
                + "\n" + DummyTarget.DescribeLatest()
                + "\n" + ShieldSoldier.DescribeLatest()
                + "\n" + RangedSoldier.DescribeLatest()
                + "\n" + AerialDrone.DescribeLatest();
        }
    }
}
