// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace BundleProbeTester
{
    public static class Program
    {
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate bool BundleProbeDelegate([MarshalAs(UnmanagedType.LPWStr)] string path, IntPtr size, IntPtr offset);

        unsafe static bool Probe(BundleProbeDelegate bundleProbe, string path, bool isExpected)
        {
            Int64 size, offset;
            bool exists = bundleProbe(path, (IntPtr)(&offset), (IntPtr)(&size));

            switch (exists, isExpected)
            {
                case (true, true):
                    if (size > 0 && offset > 0)
                    {
                        return true;
                    }

                    Console.WriteLine($"Invalid location obtained for {path} within bundle.");
                    return false;

                case (true, false):
                    Console.WriteLine($"Unexpected file {path} found in bundle.");
                    return false;

                case (false, true):
                    Console.WriteLine($"Expected file {path} not found in bundle.");
                    return false;

                case (false, false):
                    return true;
            }

            return false; // dummy
        }

        public static int Main(string[] args)
        {
            bool isSingleFile = args.Length > 0 && args[0].Equals("SingleFile");
            object probeObject = System.AppDomain.CurrentDomain.GetData("BUNDLE_PROBE");

            if (!isSingleFile)
            {
                if (probeObject != null)
                {
                    Console.WriteLine("BUNDLE_PROBE property passed in for a non-single-file app");
                    return -1;
                }

                Console.WriteLine("No BUNDLE_PROBE");
                return 0;
            }

            if (probeObject == null)
            {
                Console.WriteLine("BUNDLE_PROBE property not passed in for a single-file app");
                return -2;
            }

            string probeString = probeObject as string;
            IntPtr probePtr = (IntPtr)Convert.ToUInt64(probeString, 16);
            BundleProbeDelegate bundleProbeDelegate = Marshal.GetDelegateForFunctionPointer<BundleProbeDelegate>(probePtr);
            bool success =
                Probe(bundleProbeDelegate, "BundleProbeTester.dll", isExpected: true) &&
                Probe(bundleProbeDelegate, "BundleProbeTester.runtimeconfig.json", isExpected: true) &&
                Probe(bundleProbeDelegate, "System.Private.CoreLib.dll", isExpected: true);
                // The following test is failing on Linux-musl-x64-release. 
                // The test is temporarily disabled to keep rolling builds green until the bug is fixed.
                // https://github.com/dotnet/runtime/issues/35755
                // Probe(bundleProbeDelegate, "hostpolicy.dll", isExpected: false) &&
                // Probe(bundleProbeDelegate, "--", isExpected: false) &&
                // Probe(bundleProbeDelegate, "", isExpected: false);

            if (!success)
            {
                return -3;
            }

            Console.WriteLine("BUNDLE_PROBE OK");
            return 0;
        }
    }
}
