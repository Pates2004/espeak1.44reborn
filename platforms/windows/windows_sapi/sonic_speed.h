#pragma once

// SAPI rates are -10..+10; the native engine accepts 80..450 WPM.
namespace SonicSpeed {
enum class Mode : int { Legacy = 0, Nvda = 1 };

inline int Rate(int master, int fragment_adjustment = 0)
{
    static const int rates[21] = {80, 110, 124, 133, 142, 151, 159, 168, 174, 180, 187,
                                  196, 208, 220, 240, 270, 300, 335, 369, 390, 450};
    const long long index = static_cast<long long>(master) + fragment_adjustment + 10;
    return rates[index < 0 ? 0 : index > 20 ? 20 : static_cast<int>(index)];
}

inline int Target(int master, bool enabled, Mode mode)
{
    if (!enabled) return 0;
    int desired = 0;
    if (mode == Mode::Nvda) {
        desired = Rate(master) * 3;
    } else if (master > 0) {
        const int positive = master > 10 ? 10 : master;
        const float factor = 1.0f + (positive * 0.2f);
        desired = static_cast<int>(Rate(master) * factor + 0.5f);
    }
    return desired > 450 ? desired : 0;
}

inline int NativeRate(int master, int fragment_adjustment, bool enabled, Mode mode)
{
    const int base = Rate(master, fragment_adjustment);
    if (!enabled || mode != Mode::Nvda) return base;
    const int tripled = base * 3;
    return tripled > 450 ? 450 : tripled;
}
} // namespace SonicSpeed
