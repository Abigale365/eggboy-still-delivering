using System.Collections;
using UnityEngine;

internal sealed class DisplayQualityMonitor : MonoBehaviour
{
	public bool Check;

	private DisplayResolutionGuard.FrameBudget frameBudget;

	private float warmedFor;

	private IEnumerator Start()
	{
		if (Check)
		{
			yield return DisplayResolutionGuard.CheckOnStartup();
		}
	}

	private void Update()
	{
		if (!DisplayResolutionGuard.CanAdapt || Time.timeScale <= 0f || !Application.isFocused)
		{
			frameBudget = default(DisplayResolutionGuard.FrameBudget);
			return;
		}
		warmedFor += Time.unscaledDeltaTime;
		if (!(warmedFor < 2f) && frameBudget.Observe(Time.unscaledDeltaTime))
		{
			DisplayResolutionGuard.StepDown();
		}
	}
}
