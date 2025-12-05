# QR Code Conditional Red Pixel Removal - Exclusion Model

## Overview
The booklet processor supports conditional red pixel removal based on QR code content. Pages with QR codes matching the exclusion list will **SKIP** red pixel removal, while all others will receive it (if globally enabled).

## Decision Logic - Exclusion Model

### When QR Scanning is ENABLED:
- **QR code matches exclusion list** ? SKIP red pixel removal
- **QR code found, no match** ? APPLY red pixel removal (if enabled globally)
- **No QR code found** ? APPLY red pixel removal (if enabled globally)

### When QR Scanning is DISABLED:
- All pages use global `EnableRedPixelRemover` setting

## Configuration Example

```json
{
  "BookletProcessor": {
    "EnableRedPixelRemover": true,
    "QrScanner": {
      "EnableQrScanning": true,
      "QrValuesExcludingRedRemoval": [
        "MACHINE_SCORED",
        "NO_RED_INK",
        "CLEAN"
      ]
    }
  }
}
```

## Use Case Examples

### Use Case 1: Machine-Scored Tests (Most Common)
**Scenario**: Multiple choice tests are machine-scored and don't need red removal. Essay tests do.

**Setup**:
- Mark machine-scored tests with QR: "MACHINE_SCORED"
- Essay tests: No QR or different QR
- Config: `["MACHINE_SCORED"]`

**Result**:
- Machine-scored ? Skip (fast)
- Essay tests ? Apply (removes red marks)

### Use Case 2: Performance Optimization
**Scenario**: 70% of documents are already clean, 30% need red removal.

**Setup**:
- Mark clean documents: "CLEAN"
- Unmarked documents get full processing

**Benefit**:
- 70% skip expensive operation
- 30% get necessary processing
- Overall faster throughput

## Console Output

### Excluded (Skipped)
```
Page 1: QR code detected: MACHINE_SCORED
Page 1: QR code matches exclusion criteria - skipping red pixel removal
```

### Not Excluded (Applied)
```
Page 2: QR code detected: ESSAY_TEST
Page 2: QR code does not match exclusion criteria - applying red pixel removal
```

### No QR Code (Default Behavior)
```
Page 3: No QR code detected - using default red pixel removal setting
```

## Key Differences from "Required" Model

| Aspect | Exclusion Model (Current) | Required Model (Old) |
|--------|---------------------------|----------------------|
| Default behavior | Apply red removal | Skip red removal |
| QR code purpose | Mark exceptions | Mark required pages |
| Configuration key | `QrValuesExcludingRedRemoval` | `QrValuesRequiringRedRemoval` |
| Typical use | Mark clean pages | Mark dirty pages |
| Pages needing QR | Minority (exceptions) | Majority (all needing processing) |

## Configuration Parameters

### QR Scanner Settings
- `EnableQrScanning` (bool): Enable/disable QR feature
- `QrRegionX`, `QrRegionY`, `QrRegionWidth`, `QrRegionHeight` (int): Scan region
- `QrValuesExcludingRedRemoval` (array): QR values that prevent red removal

### Default Values
```csharp
qrValuesExcludingRedRemoval: ["MACHINE_SCORED", "NO_RED_INK", "CLEAN"]
qrRegionX: 1950, qrRegionY: 2700, qrRegionWidth: 600, qrRegionHeight: 600
// Lower right corner, 2x2 inches @ 300 DPI
```

## Best Practices

1. **Use exclusion for minority cases**
   - Mark the 20% that don't need processing
   - Not the 80% that do

2. **Descriptive exclusion codes**
   - "MACHINE_SCORED" ?
   - "MS" ?

3. **Keep list minimal**
   - Only add genuine exclusions
   - Review periodically

4. **Monitor performance**
   - Count skipped vs processed pages
   - Verify expected benefit

## Troubleshooting

### Pages skipped unexpectedly
- Check exclusion list values
- Verify QR codes on pages
- Enable console logging

### Pages processed unexpectedly
- Verify QR codes contain expected values
- Check case-insensitive matching
- Verify global red removal is enabled

### QR not detected
- Check region coordinates
- Verify image resolution
- Test QR code quality

## Testing Recommendations

1. Test with QR code: "MACHINE_SCORED" ? Should skip
2. Test with QR code: "TEACHER_MARKED" ? Should apply
3. Test without QR code ? Should apply (if globally enabled)
4. Verify console messages match expectations

## Summary

**Exclusion Model** = Default to processing, mark exceptions
- More efficient for typical scenarios
- Fewer QR codes needed
- Clearer intent (marks what's different)
- Better performance (skip unnecessary work)
