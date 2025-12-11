# Regional Red Pixel Removal Exclusion - Implementation Plan

## Quick Overview

**Goal**: Allow specific regions of an image (defined by x, y, width, height in inches) to be excluded from red pixel removal, while removing red from the rest of the image.

**Use Cases**:
- Preserve red signatures in signature boxes
- Keep red markings in answer key areas
- Protect color-coded instruction sections
- Preserve red logos or headers

## ? Feasibility: YES

This is a straightforward image processing task using a mask-based approach.

## Recommended Approach: Mask-Based Processing

### How It Works

1. **Create Boolean Mask**: Build a 2D array marking which pixels to protect
2. **Mark Exclusion Regions**: Set mask to `true` for pixels in exclusion regions
3. **Process with Mask**: Skip red removal for masked pixels

### Advantages
- ? Clean, efficient single-pass processing
- ? O(1) lookup per pixel
- ? Minimal memory overhead (~8 MB for typical image)
- ? Simple to understand and maintain

## Configuration Format

```json
{
  "BookletProcessor": {
    "RedPixelRemoval": {
      "ExclusionRegions": [
        {
          "Name": "Signature Box",
          "XInches": 1.0,
          "YInches": 10.0,
          "WidthInches": 3.0,
          "HeightInches": 0.75
        }
      ]
    }
  }
}
```

**Coordinate System**: Top-left origin (0,0), measured in inches

## Implementation Phases

### Phase 1: Core (Essential)
1. Create `RedPixelExclusionRegion` model
2. Add interface overload with regions parameter
3. Implement mask generation
4. Update `RedPixelRemoverService` with mask-based logic

### Phase 2: Integration (Required)
5. Update service constructors to accept regions
6. Load configuration in MainWindow and Console
7. Pass regions to red removal calls
8. Update both processing modes

### Phase 3: UI & Docs (Nice to Have)
9. Display regions in SettingsWindow (read-only)
10. Create documentation with examples
11. Add validation and error handling

## Code Structure

### New Model Class
```csharp
public class RedPixelExclusionRegion
{
    public string Name { get; set; }
    public double XInches { get; set; }
    public double YInches { get; set; }
    public double WidthInches { get; set; }
    public double HeightInches { get; set; }
    
    public Rectangle ToPixelRectangle(int dpi) { ... }
}
```

### Updated Interface
```csharp
public interface IRedPixelRemoverService
{
    // Existing method (backwards compatible)
    Task RemoveRedPixelsAsync(...);
    
    // New overload with exclusion regions
    Task RemoveRedPixelsAsync(..., List<RedPixelExclusionRegion> exclusionRegions);
}
```

### Core Algorithm
```csharp
// 1. Create mask
bool[,] mask = CreateExclusionMask(width, height, regions, dpi);

// 2. Process with mask check
for (int y = 0; y < height; y++)
{
    for (int x = 0; x < width; x++)
    {
        if (mask[y, x]) continue; // Skip protected pixels
        
        if (IsRedPixel(pixel))
            pixel = White;
    }
}
```

## Configuration Examples

### Example 1: Single Signature Box
```json
{
  "ExclusionRegions": [
    {
      "Name": "Student Signature",
      "XInches": 1.0,
      "YInches": 10.0,
      "WidthInches": 3.0,
      "HeightInches": 0.75
    }
  ]
}
```

### Example 2: Multiple Protected Areas
```json
{
  "ExclusionRegions": [
    {
      "Name": "Header Logo",
      "XInches": 0.5,
      "YInches": 0.5,
      "WidthInches": 2.0,
      "HeightInches": 1.0
    },
    {
      "Name": "Answer Key",
      "XInches": 7.0,
      "YInches": 1.0,
      "WidthInches": 1.5,
      "HeightInches": 9.0
    }
  ]
}
```

## Performance Impact

| Metric | Impact |
|--------|--------|
| **Memory** | +8 MB for typical 2550×3300 image |
| **Speed** | ~1-2% slower (negligible) |
| **Complexity** | Low (simple boolean check) |

## Backwards Compatibility

? **Fully Compatible**
- New overload doesn't break existing code
- Empty regions list = current behavior
- No configuration = current behavior

## Testing Strategy

### Visual Test
1. Create test image with red text in multiple locations
2. Define exclusion region around specific red text
3. Process image
4. Verify:
   - Red text in exclusion region **preserved** ?
   - Red text outside region **removed** ?

### Unit Tests
- Single region
- Multiple regions
- Overlapping regions
- Out-of-bounds regions
- Empty regions list

## Visual Diagram

```
???????????????????????????????????????
? 8.5" × 11" Document                 ?
?                                     ?
?  ????????????????  ? Logo           ?
?  ? PROTECTED    ?     (preserved)   ?
?  ? Red Logo     ?                   ?
?  ????????????????                   ?
?                                     ?
?  Red text removed ? Processing      ?
?  Red marks removed                  ?
?                                     ?
?              ????????????????????   ?
?              ? PROTECTED        ?   ?
?              ? Signature Box    ?   ?
?              ? Red ink preserved?   ?
?              ????????????????????   ?
???????????????????????????????????????
```

## Next Steps

1. **Review this plan** - Feedback on approach and design
2. **Confirm use cases** - Specific scenarios you need
3. **Decide on phases** - Which phases to implement now vs later
4. **Coordinate system** - Confirm inch-based coordinates work for you

## Key Decisions Needed

### Question 1: Scope
- Implement full plan or just core functionality?
- UI display needed now or later?

### Question 2: Configuration
- Per-template regions or global regions?
- Static config or support dynamic regions based on QR codes?

### Question 3: Validation
- Strict validation (error on invalid) or lenient (warn and continue)?
- Auto-clip regions to image bounds?

### Question 4: Performance
- Is 8 MB memory overhead acceptable?
- Need optimization for very large images?

## Estimated Implementation Time

- **Phase 1 (Core)**: 2-3 hours
- **Phase 2 (Integration)**: 1-2 hours  
- **Phase 3 (UI & Docs)**: 1-2 hours
- **Total**: 4-7 hours

## Recommended Priority

**High Priority** if you need:
- Signature preservation
- Answer key protection
- Logo/branding preservation

**Medium Priority** if:
- Nice to have but not critical
- Can manually edit images if needed

**Low Priority** if:
- Current red removal works fine
- No specific regions need protection

---

**Ready to proceed?** Let me know your feedback and I can implement this feature!
