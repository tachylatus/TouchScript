﻿using System;

namespace TouchScript.Utils
{
    static internal class EventHandlerExtensions
    {
        static public Exception InvokeHandleExceptions<T>(this EventHandler<T> handler, object sender, T args)
            where T : EventArgs
        {
            try
            {
                handler(sender, args);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                return ex;
            }
            return null;
        }

        static public Exception InvokeHandleExceptions(this EventHandler handler, object sender, EventArgs args)
        {
            try
            {
                handler(sender, args);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                return ex;
            }
            return null;
        }
    }
}
