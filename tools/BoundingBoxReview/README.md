# 🟨🟥 Bounding box review

A developer tool for tuning the motion detector's bounding boxes on real recordings: the detector draws its boxes (yellow), a person draws where they should be (red), and the tool compares the two. Never shipped.

## 🚀 Run it

```powershell
dotnet run --project tools/BoundingBoxReview -c Release -- "<recording.mkv or review folder>"
```

or start it without a path and use *Open recording...* / *Open review folder...*.

## 🔁 The process

1. **Make the pictures.** *Open recording...* runs the recording through the detector as CameraWall does live (the same detector, settings and analyses a second) and writes a picture every *Every* seconds into `{recording}-review` beside it. The motion settings come from the first `settings.json` found in the recording's folder or above it (copy a CameraWall data folder as it is), else the defaults; *Sensitivity* overrides theirs.
2. **Draw the boxes.** For each picture, drag a red box around every thing that should be boxed (a person, a cat). Right-click a red box to remove it, *Ctrl+Z* removes the last one, *Ctrl+N* says nothing should be boxed, arrows step through the pictures. Write what you see in the comment box ("one cat, sitting very still", "the cat is moving"); leave it empty when nothing changed since the picture before. Every change is saved at once.
3. **Compare.** *Compare* writes `compare.md`: per picture the overlap (IoU), the size of the detector's box against yours, how far each edge lies outside yours, what it missed and what it boxed that you did not; and a summary.

## 🔁 After changing the detector

*Run detector again* runs today's detector over the review's recording with the review's settings and gives every picture the new yellow boxes; your red boxes and comments stay, and *Compare* runs at once. So each review is a test of the next change. Without a window:

```powershell
BoundingBoxReview.exe --rerun "<review folder>"                  # new boxes, compare.md, rerun.log
BoundingBoxReview.exe --masks "<review folder>" [--no-ghosts]    # what the detector saw: masks\ (foreground in magenta, every blob and its fate)
```

## 📁 The review folder

| File | What |
|---|---|
| `boxes.json` | The recording, the settings, and per picture the detector's boxes (`detector`) and yours (`person`: absent until reviewed, empty for *nothing here*), in the video's pixels as displayed, and your comment (`comment`, absent when empty) |
| `{name}-tNNNs.png` | The picture with the detector's boxes in yellow and yours in red |
| `original/{name}-tNNNs.png` | The picture without boxes |
| `compare.md` | The comparison |

A picture drawn on in red in another program (and not reviewed in the tool) is read from its red pixels.

## ⚠️ Good to know

- The detector starts cold on the recording and only learns the scene for its first 3 seconds, as *Analyse recording* and the playback boxes do. Live, CameraWall had learnt the scene long before the recording started, so the first seconds can differ from what was shown live.
- The boxes are the detector's own, without the overlay's smoothing.
