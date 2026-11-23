using System;
using UnityEngine;

public static class GameEvents
{
    public static Action<int, string> OnScore;
    public static Action<string> OnToast;
    public static Action<string> OnAlert;          // red warning on screen
    public static Action<Vector3, string> OnCrime; // where and what
    public static Action<string> OnSnitched;       // who snitched
}