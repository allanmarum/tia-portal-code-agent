using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            ShowUsage();
            return 1;
        }

        string mode = args[0].ToLowerInvariant();
        if (mode == "-h" || mode == "--help" || mode == "help")
        {
            ShowUsage();
            return 0;
        }

        if (mode == "verify")
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("[FAIL] Missing addin file path for verify.");
                ShowUsage();
                return 1;
            }
            return VerifyPackageSignature(args[1]);
        }

        string addinPath;
        string explicitThumbprint = null;
        string pfxPath = null;
        string pfxPassword = null;
        bool requireSigning = IsEnvTrue("TIA_REQUIRE_SIGNING");
        bool allowSelfSigned = !requireSigning;

        if (mode == "sign")
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("[FAIL] Missing addin file path for sign.");
                ShowUsage();
                return 1;
            }
            addinPath = args[1];

            for (int i = 2; i < args.Length; i++)
            {
                string arg = args[i];
                if ((arg == "--thumbprint" || arg == "-t") && i + 1 < args.Length)
                {
                    explicitThumbprint = args[++i];
                }
                else if ((arg == "--pfx" || arg == "-p") && i + 1 < args.Length)
                {
                    pfxPath = args[++i];
                }
                else if ((arg == "--password" || arg == "-pwd") && i + 1 < args.Length)
                {
                    pfxPassword = args[++i];
                }
                else if (arg == "--require-signed")
                {
                    requireSigning = true;
                    allowSelfSigned = false;
                }
                else if (arg == "--allow-self-signed")
                {
                    allowSelfSigned = true;
                }
            }
        }
        else
        {
            // Backward compatibility: first arg is file path, optional second arg is thumbprint
            addinPath = args[0];
            if (args.Length > 1)
            {
                explicitThumbprint = args[1];
            }
        }

        return SignPackage(addinPath, explicitThumbprint, pfxPath, pfxPassword, requireSigning, allowSelfSigned);
    }

    static int SignPackage(
        string addinPath,
        string? explicitThumbprint,
        string? pfxPath,
        string? pfxPassword,
        bool requireSigning,
        bool allowSelfSigned)
    {
        if (!File.Exists(addinPath))
        {
            Console.Error.WriteLine("[FAIL] File not found: " + addinPath);
            return 1;
        }

        X509Certificate2 cert = ResolveCertificate(explicitThumbprint, pfxPath, pfxPassword, allowSelfSigned);

        if (cert == null)
        {
            if (requireSigning)
            {
                Console.Error.WriteLine("[FAIL] Signing certificate or key material not found. Release signing is mandatory.");
                return 1;
            }

            Console.WriteLine("[WARN] No certificate found. Package remains unsigned (RequireSigning=false).");
            return 0;
        }

        Console.WriteLine("Certificate Subject: " + cert.Subject);
        Console.WriteLine("Certificate Thumbprint: " + cert.Thumbprint);
        Console.WriteLine("Signing package: " + addinPath);

        try
        {
            using (Package package = Package.Open(addinPath, FileMode.Open, FileAccess.ReadWrite))
            {
                PackageDigitalSignatureManager sigManager = new PackageDigitalSignatureManager(package);

                List<Uri> partUris = new List<Uri>();
                foreach (PackagePart part in package.GetParts())
                {
                    partUris.Add(part.Uri);
                }

                Console.WriteLine("  Signing " + partUris.Count + " package parts...");
                sigManager.Sign(partUris, cert);
            }

            Console.WriteLine("[PASS] Package signed successfully.");

            // Self-verify the signature immediately
            return VerifyPackageSignature(addinPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[FAIL] Error signing package: " + ex.Message);
            return 1;
        }
    }

    static int VerifyPackageSignature(string addinPath)
    {
        if (!File.Exists(addinPath))
        {
            Console.Error.WriteLine("[FAIL] File not found for signature verification: " + addinPath);
            return 1;
        }

        try
        {
            using (Package package = Package.Open(addinPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                PackageDigitalSignatureManager sigManager = new PackageDigitalSignatureManager(package);
                if (!sigManager.IsSigned)
                {
                    Console.Error.WriteLine("[FAIL] Package is unsigned: " + addinPath);
                    return 1;
                }

                VerifyResult result = sigManager.VerifySignatures(false);
                if (result != VerifyResult.Success)
                {
                    Console.Error.WriteLine("[FAIL] Package digital signature verification failed: " + result);
                    return 1;
                }

                Console.WriteLine("[PASS] Package signature verification succeeded for: " + addinPath);
                foreach (PackageDigitalSignature sig in sigManager.Signatures)
                {
                    if (sig.Signer != null)
                    {
                        Console.WriteLine("  Signer: " + sig.Signer.Subject + " [" + sig.Signer.GetCertHashString() + "]");
                    }
                }

                return 0;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("[FAIL] Exception during signature verification: " + ex.Message);
            return 1;
        }
    }

    static X509Certificate2? ResolveCertificate(
        string? explicitThumbprint,
        string? pfxPath,
        string? pfxPassword,
        bool allowSelfSigned)
    {
        string thumbprint = explicitThumbprint ?? Environment.GetEnvironmentVariable("TIA_SIGNING_CERT_THUMBPRINT");
        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            X509Certificate2 cert = FindCertificateByThumbprint(thumbprint.Trim());
            if (cert != null) return cert;
        }

        string envPfxPath = pfxPath ?? Environment.GetEnvironmentVariable("TIA_SIGNING_CERT_PFX");
        string password = pfxPassword ?? Environment.GetEnvironmentVariable("TIA_SIGNING_CERT_PASSWORD") ?? "";

        if (!string.IsNullOrWhiteSpace(envPfxPath) && File.Exists(envPfxPath))
        {
            X509Certificate2 cert = LoadPfxFromFile(envPfxPath, password);
            if (cert != null) return cert;
        }

        string envPfxBase64 = Environment.GetEnvironmentVariable("TIA_SIGNING_CERT_PFX_BASE64");
        if (!string.IsNullOrWhiteSpace(envPfxBase64))
        {
            X509Certificate2 cert = LoadPfxFromBase64(envPfxBase64, password);
            if (cert != null) return cert;
        }

        if (allowSelfSigned)
        {
            return FindOrCreateSelfSignedCert();
        }

        return null;
    }

    static X509Certificate2 FindCertificateByThumbprint(string thumbprint)
    {
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        try
        {
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (found.Count > 0) return found[0];
        }
        finally
        {
            store.Close();
        }

        X509Store lmStore = new X509Store(StoreName.My, StoreLocation.LocalMachine);
        try
        {
            lmStore.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection found = lmStore.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
            if (found.Count > 0) return found[0];
        }
        finally
        {
            lmStore.Close();
        }

        return null;
    }

    static X509Certificate2 LoadPfxFromFile(string pfxPath, string password)
    {
        try
        {
            return new X509Certificate2(pfxPath, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }
        catch
        {
            try
            {
                return new X509Certificate2(pfxPath, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[WARN] Failed to load PFX file: " + ex.Message);
                return null;
            }
        }
    }

    static X509Certificate2 LoadPfxFromBase64(string base64, string password)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return new X509Certificate2(bytes, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }
        catch
        {
            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                return new X509Certificate2(bytes, password, X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.Exportable);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[WARN] Failed to load PFX from base64 string: " + ex.Message);
                return null;
            }
        }
    }

    static X509Certificate2 FindOrCreateSelfSignedCert()
    {
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly);

        X509Certificate2Collection existing = store.Certificates.Find(
            X509FindType.FindBySubjectName, "TIA Portal Code Agent", false);
        if (existing.Count > 0)
        {
            X509Certificate2 cert = existing[0];
            store.Close();
            return cert;
        }
        store.Close();

        Console.WriteLine("Creating self-signed code signing certificate...");

        RSA rsa = RSA.Create(2048);
        CertificateRequest request = new CertificateRequest(
            new X500DistinguishedName("CN=TIA Portal Code Agent"),
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                false));

        X509Certificate2 newCert = request.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(5));

        X509Store writableStore = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        writableStore.Open(OpenFlags.ReadWrite);
        writableStore.Add(newCert);
        writableStore.Close();

        Console.WriteLine("  Created self-signed cert: " + newCert.Thumbprint);
        return newCert;
    }

    static bool IsEnvTrue(string varName)
    {
        string val = Environment.GetEnvironmentVariable(varName);
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(val, "1", StringComparison.OrdinalIgnoreCase);
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  OpcSigner sign <addin-file> [--thumbprint <thumbprint>] [--pfx <path>] [--password <pwd>] [--require-signed]");
        Console.WriteLine("  OpcSigner verify <addin-file>");
        Console.WriteLine("  OpcSigner <addin-file> [thumbprint]");
    }
}

