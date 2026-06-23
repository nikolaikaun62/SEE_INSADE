using ComputeSharp;

namespace SEE_INSADE.Core.Imaging.Gpu
{
    // Material codes are generated in GpuImageProcessor.ToGpuMaterialCode.
    // Color is packed as 0x00RRGGBB and later copied into Bgr32 bytes.
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct GpuOperatorFilterKernel(
        ReadOnlyBuffer<int> materials,
        ReadOnlyBuffer<float> densities,
        ReadWriteBuffer<int> output,
        int width,
        int height,
        int mode,
        float strength,
        int brightnessEnabled,
        float brightness,
        int contrastEnabled,
        float contrast,
        int materialEnhancementEnabled,
        int edgeDetectionEnabled,
        int noiseReductionEnabled) : IComputeShader
    {
        public void Execute()
        {
            int index = ThreadIds.X;
            int x = index % width;
            int y = index / width;
            int material = materials[index];
            float density = Clamp(densities[index], 0.0f, 2.35f);

            float r;
            float g;
            float b;
            GetXrayColor(material, density, out r, out g, out b);

            ApplyMode(material, density, x, y, mode, strength, ref r, ref g, ref b);

            if (noiseReductionEnabled == 1)
            {
                float nr;
                float ng;
                float nb;
                GetNeighborhoodAverage(x, y, out nr, out ng, out nb);
                Blend(ref r, ref g, ref b, nr, ng, nb, 0.35f);
            }

            if (materialEnhancementEnabled == 1)
                BoostSaturation(ref r, ref g, ref b, 0.35f + density * 0.35f);

            if (edgeDetectionEnabled == 1)
                ApplyEdge(x, y, 1.6f, ref r, ref g, ref b);

            if (brightnessEnabled == 1)
                ApplyBrightness(ref r, ref g, ref b, brightness);

            if (contrastEnabled == 1)
                ApplyContrast(ref r, ref g, ref b, contrast);

            output[index] = Pack(r, g, b);
        }

        private void ApplyMode(int material, float density, int x, int y, int renderMode, float renderStrength, ref float r, ref float g, ref float b)
        {
            // Keep these numeric values aligned with OperatorFilterMode enum declaration.
            if (renderMode == 0) // EnhancedColor
            {
                BoostSaturation(ref r, ref g, ref b, 0.35f * renderStrength);
            }
            else if (renderMode == 1) // HighPenetration
            {
                HighPenetration(material, density, renderStrength, out r, out g, out b);
            }
            else if (renderMode == 2) // OrganicFocus
            {
                FocusMaterial(IsOrganic(material), renderStrength, ref r, ref g, ref b);
            }
            else if (renderMode == 3) // InorganicFocus
            {
                FocusMaterial(IsInorganic(material), renderStrength, ref r, ref g, ref b);
            }
            else if (renderMode == 4) // MetalFocus
            {
                FocusMaterial(IsMetal(material) || material == 12, renderStrength, ref r, ref g, ref b);
            }
            else if (renderMode == 5) // DensityMap
            {
                float value = 255.0f - Clamp(density / 2.35f, 0.0f, 1.0f) * 255.0f;
                r = value;
                g = value;
                b = value;
            }
            else if (renderMode == 6) // Negative
            {
                r = 255.0f - r;
                g = 255.0f - g;
                b = 255.0f - b;
            }
            else if (renderMode == 7) // Threshold
            {
                if (material == 0)
                {
                    r = 255.0f;
                    g = 255.0f;
                    b = 255.0f;
                }
                else
                {
                    float threshold = Clamp(0.22f + renderStrength * 0.18f, 0.24f, 0.82f);
                    float value = density >= threshold ? 0.0f : 232.0f;
                    r = value;
                    g = value;
                    b = value + 4.0f;
                }
            }
            else if (renderMode == 8) // EdgeEmphasis
            {
                ApplyEdge(x, y, renderStrength, ref r, ref g, ref b);
            }
            else if (renderMode == 9) // SuspectHighlight
            {
                SuspectHighlight(material, density, renderStrength, ref r, ref g, ref b);
            }
        }

        private void GetXrayColor(int material, float density, out float r, out float g, out float b)
        {
            if (material == 0)
            {
                r = 255.0f;
                g = 255.0f;
                b = 255.0f;
                return;
            }

            float br;
            float bg;
            float bb;
            GetMaterialColor(material, out br, out bg, out bb);

            float opacity = Clamp(0.22f + density * 0.58f, 0.0f, 1.0f);
            float darkening = IsMetal(material)
                ? Clamp(density * 0.42f, 0.0f, 0.72f)
                : Clamp(density * 0.22f, 0.0f, 0.42f);

            if (material == 14)
                darkening = Clamp(density * 0.78f, 0.0f, 0.90f);

            if (material == 15)
                darkening = Clamp(density * 0.72f, 0.0f, 0.90f);

            r = BlendChannel(255.0f, br, opacity, darkening);
            g = BlendChannel(255.0f, bg, opacity, darkening);
            b = BlendChannel(255.0f, bb, opacity, darkening);
        }

        private static void GetMaterialColor(int material, out float r, out float g, out float b)
        {
            r = 245.0f;
            g = 248.0f;
            b = 252.0f;

            if (material == 1) { r = 238.0f; g = 138.0f; b = 48.0f; }
            else if (material == 2) { r = 255.0f; g = 203.0f; b = 92.0f; }
            else if (material == 3) { r = 238.0f; g = 139.0f; b = 55.0f; }
            else if (material == 4) { r = 235.0f; g = 183.0f; b = 76.0f; }
            else if (material == 5) { r = 92.0f; g = 176.0f; b = 108.0f; }
            else if (material == 6) { r = 108.0f; g = 190.0f; b = 96.0f; }
            else if (material == 7) { r = 86.0f; g = 175.0f; b = 124.0f; }
            else if (material == 8) { r = 76.0f; g = 165.0f; b = 82.0f; }
            else if (material == 9) { r = 72.0f; g = 142.0f; b = 232.0f; }
            else if (material == 10) { r = 36.0f; g = 101.0f; b = 194.0f; }
            else if (material == 11) { r = 38.0f; g = 72.0f; b = 142.0f; }
            else if (material == 12) { r = 50.0f; g = 150.0f; b = 116.0f; }
            else if (material == 13) { r = 20.0f; g = 38.0f; b = 86.0f; }
            else if (material == 14) { r = 120.0f; g = 78.0f; b = 8.0f; }
            else if (material == 15) { r = 12.0f; g = 18.0f; b = 28.0f; }
        }

        private void GetNeighborhoodAverage(int x, int y, out float r, out float g, out float b)
        {
            r = 0.0f;
            g = 0.0f;
            b = 0.0f;
            float count = 0.0f;

            for (int yy = -1; yy <= 1; yy++)
            {
                for (int xx = -1; xx <= 1; xx++)
                {
                    int sx = ClampInt(x + xx, 0, width - 1);
                    int sy = ClampInt(y + yy, 0, height - 1);
                    int sampleIndex = sy * width + sx;
                    float sr;
                    float sg;
                    float sb;
                    GetXrayColor(materials[sampleIndex], densities[sampleIndex], out sr, out sg, out sb);
                    r += sr;
                    g += sg;
                    b += sb;
                    count += 1.0f;
                }
            }

            r /= count;
            g /= count;
            b /= count;
        }

        private void ApplyEdge(int x, int y, float renderStrength, ref float r, ref float g, ref float b)
        {
            int index = y * width + x;
            int rightIndex = y * width + ClampInt(x + 1, 0, width - 1);
            int downIndex = ClampInt(y + 1, 0, height - 1) * width + x;

            float center = densities[index];
            float right = densities[rightIndex];
            float down = densities[downIndex];
            int material = materials[index];

            float edge = Abs(center - right) + Abs(center - down);
            if (materials[rightIndex] != material)
                edge += 0.45f;
            if (materials[downIndex] != material)
                edge += 0.45f;

            edge = Clamp(edge * renderStrength, 0.0f, 1.0f);

            if (edge > 0.18f)
                Blend(ref r, ref g, ref b, 20.0f, 30.0f, 45.0f, edge);
            else
                Blend(ref r, ref g, ref b, 255.0f, 255.0f, 255.0f, 0.15f);
        }

        private static void FocusMaterial(bool isTarget, float renderStrength, ref float r, ref float g, ref float b)
        {
            if (isTarget)
            {
                BoostSaturation(ref r, ref g, ref b, 0.55f * renderStrength);
                return;
            }

            float gray = ToGray(r, g, b);
            float faded = Clamp(gray + 38.0f, 0.0f, 255.0f);
            r = faded;
            g = faded;
            b = faded;
        }

        private static void HighPenetration(int material, float density, float renderStrength, out float r, out float g, out float b)
        {
            if (material == 0)
            {
                r = 255.0f;
                g = 255.0f;
                b = 255.0f;
                return;
            }

            float penetration = Clamp(density * renderStrength, 0.0f, 1.0f);
            float shade = Clamp(255.0f - penetration * 235.0f, 0.0f, 255.0f);

            if (material == 14)
            {
                r = shade * 0.22f;
                g = shade * 0.16f;
                b = shade * 0.08f;
            }
            else if (material == 15)
            {
                r = shade * 0.10f;
                g = shade * 0.12f;
                b = shade * 0.16f;
            }
            else if (material == 11 || material == 13)
            {
                r = shade * 0.28f;
                g = shade * 0.36f;
                b = shade;
            }
            else if (material == 9 || material == 10)
            {
                r = shade * 0.42f;
                g = shade * 0.56f;
                b = shade;
            }
            else
            {
                r = shade;
                g = shade;
                b = shade;
            }
        }

        private static void SuspectHighlight(int material, float density, float renderStrength, ref float r, ref float g, ref float b)
        {
            bool suspect = (material == 1 || material == 2 || material == 11 || material == 12 || material == 13 || material == 14 || material == 15) && density > 0.55f;

            if (!suspect)
            {
                FocusMaterial(false, renderStrength, ref r, ref g, ref b);
                return;
            }

            float ar = (material == 1 || material == 2) ? 255.0f : 230.0f;
            float ag = (material == 1 || material == 2) ? 112.0f : 32.0f;
            float ab = (material == 1 || material == 2) ? 31.0f : 58.0f;

            Blend(ref r, ref g, ref b, ar, ag, ab, Clamp(0.35f + density * 0.45f, 0.0f, 0.85f));
        }

        private static void ApplyBrightness(ref float r, ref float g, ref float b, float factor)
        {
            r = Clamp(r * factor, 0.0f, 255.0f);
            g = Clamp(g * factor, 0.0f, 255.0f);
            b = Clamp(b * factor, 0.0f, 255.0f);
        }

        private static void ApplyContrast(ref float r, ref float g, ref float b, float factor)
        {
            r = Clamp(((r / 255.0f - 0.5f) * factor + 0.5f) * 255.0f, 0.0f, 255.0f);
            g = Clamp(((g / 255.0f - 0.5f) * factor + 0.5f) * 255.0f, 0.0f, 255.0f);
            b = Clamp(((b / 255.0f - 0.5f) * factor + 0.5f) * 255.0f, 0.0f, 255.0f);
        }

        private static void BoostSaturation(ref float r, ref float g, ref float b, float amount)
        {
            float gray = ToGray(r, g, b);
            r = Clamp(gray + (r - gray) * (1.0f + amount), 0.0f, 255.0f);
            g = Clamp(gray + (g - gray) * (1.0f + amount), 0.0f, 255.0f);
            b = Clamp(gray + (b - gray) * (1.0f + amount), 0.0f, 255.0f);
        }

        private static void Blend(ref float r, ref float g, ref float b, float sr, float sg, float sb, float amount)
        {
            amount = Clamp(amount, 0.0f, 1.0f);
            float inverse = 1.0f - amount;
            r = Clamp(r * inverse + sr * amount, 0.0f, 255.0f);
            g = Clamp(g * inverse + sg * amount, 0.0f, 255.0f);
            b = Clamp(b * inverse + sb * amount, 0.0f, 255.0f);
        }

        private static float BlendChannel(float background, float foreground, float opacity, float darkening)
        {
            float value = background * (1.0f - opacity) + foreground * opacity;
            value *= 1.0f - darkening;
            return Clamp(value, 0.0f, 255.0f);
        }

        private static float ToGray(float r, float g, float b)
        {
            return Clamp(r * 0.299f + g * 0.587f + b * 0.114f, 0.0f, 255.0f);
        }

        private static int Pack(float r, float g, float b)
        {
            int ri = (int)Clamp(r, 0.0f, 255.0f);
            int gi = (int)Clamp(g, 0.0f, 255.0f);
            int bi = (int)Clamp(b, 0.0f, 255.0f);
            return bi | (gi << 8) | (ri << 16);
        }

        private static bool IsOrganic(int material)
        {
            return material == 1 || material == 2 || material == 3 || material == 4;
        }

        private static bool IsInorganic(int material)
        {
            return material == 5 || material == 6 || material == 7 || material == 9 || material == 10;
        }

        private static bool IsMetal(int material)
        {
            return material == 9 || material == 10 || material == 11 || material == 13 || material == 14 || material == 15;
        }

        private static float Abs(float value)
        {
            return value < 0.0f ? -value : value;
        }

        private static float Clamp(float value, float min, float max)
        {
            return Hlsl.Min(Hlsl.Max(value, min), max);
        }

        private static int ClampInt(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
