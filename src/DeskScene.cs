using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PenguinPet {
    // BongoCat standard-mode semantics: keyboard paw + independent pointer paw.
    // Articulated textures let both input streams animate at the same time.
    sealed class DeskScene : IDisposable {
        const int CoatX = 98, CoatY = 184, CoatWidth = 225, CoatHeight = 82;
        readonly Bitmap coat;
        readonly bool videoModel;
        readonly Bitmap[] heads=new Bitmap[6];
        readonly FurTexture leftFur, rightFur;
        readonly List<KeyCap> keys = new List<KeyCap>();
        PointF leftTip = new PointF(251,262), rightTip = new PointF(93,249);
        PointF mouseLean;
        double lastFrame = -1;
        sealed class KeyCap {
            public int Vk; public string Label; public RectangleF Rect;
            public KeyCap(int vk, string label, float x, float y, float w) { Vk=vk; Label=label; Rect=new RectangleF(x,y,w,12); }
        }
        public DeskScene(Bitmap[] images,Bitmap[] expressions) {
            using (var bodyStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PenguinPet.VideoBody"))
            using (var faceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PenguinPet.VideoFaces")) {
                videoModel = bodyStream != null && faceStream != null;
                if (videoModel) {
                    using (var body = new Bitmap(bodyStream))
                    using (var faces = new Bitmap(faceStream))
                    using (var mouseSleeve = faces.Clone(new Rectangle(1092, 596, 154, 354), PixelFormat.Format32bppPArgb))
                    using (var keyboardSleeve = faces.Clone(new Rectangle(1317, 596, 151, 354), PixelFormat.Format32bppPArgb)) {
                        coat = MakeVideoCoat(body);
                        MakeVideoHeads(faces);
                        leftFur = new FurTexture(keyboardSleeve);
                        rightFur = new FurTexture(mouseSleeve);
                    }
                } else {
                    coat = MakeCoat(images[0]);
                    MakeClassicHeads(expressions);
                    leftFur = new FurTexture(images[2]);
                    rightFur = new FurTexture(images[3]);
                }
            }
            AddKeys();
        }
        void MakeClassicHeads(Bitmap[] expressions) {
            for(int i=0;i<6;i++) {
                heads[i]=new Bitmap(240,177,PixelFormat.Format32bppPArgb);
                using(var hg=Graphics.FromImage(heads[i])) { hg.InterpolationMode=InterpolationMode.HighQualityBicubic; hg.DrawImage(expressions[i],new RectangleF(14,8,211,271)); }
                // Feather the neck into the fixed torso rather than cutting it.
                var data=heads[i].LockBits(new Rectangle(0,0,240,177),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
                int[] pixels=new int[240*177]; Marshal.Copy(data.Scan0,pixels,0,pixels.Length);
                for(int y=165;y<177;y++) for(int x=0;x<240;x++) {
                    float a=(177-y)/12f; int color=pixels[y*240+x],result=0;
                    for(int shift=0;shift<=24;shift+=8) result|=((int)(((color>>shift)&255)*a))<<shift;
                    pixels[y*240+x]=result;
                }
                Marshal.Copy(pixels,0,data.Scan0,pixels.Length); heads[i].UnlockBits(data);
            }
        }
        static Bitmap MakeVideoCoat(Bitmap body) {
            var coat = new Bitmap(CoatWidth * 2, CoatHeight * 2, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(coat)) {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                // Register the collar and shoulders with the pose atlas.
                var target = new RectangleF(18 * 2, (35 - CoatY) * 2, 190 * 2, 322 * 2);
                g.DrawImage(body, target, new RectangleF(840, 41, 558, 950), GraphicsUnit.Pixel);
            }
            return coat;
        }
        void MakeVideoHeads(Bitmap atlas) {
            int[] cells = { 2, 1, 0, 2, 3, 4 };
            for (int i = 0; i < heads.Length; i++) {
                int cell = cells[i];
                heads[i] = new Bitmap(240, 218, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(heads[i])) {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    var source = new RectangleF(cell % 3 * 512 + 36, cell / 3 * 512 + 18, 441, 466);
                    g.DrawImage(atlas, new RectangleF(25, 6, 190, 201), source, GraphicsUnit.Pixel);
                }
                var data = heads[i].LockBits(new Rectangle(0, 0, 240, 218), ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
                try {
                    var pixels = new int[240 * 218];
                    Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                    for (int y = 168; y < 218; y++) {
                        for (int x = 0; x < 240; x++) {
                            // Keep the clasp in the expression layer. The shoulder
                            // contours come from the continuous torso texture.
                            float outsideClasp = SmoothStep((Math.Abs(x - 120) - 27) / 35f);
                            float fadeStart = 173 - 5 * outsideClasp;
                            float fadeLength = 22 - 13 * outsideClasp;
                            float opacity = 1 - SmoothStep((y - fadeStart) / fadeLength);
                            pixels[y * 240 + x] = ScalePixel(pixels[y * 240 + x], opacity);
                        }
                    }
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                } finally { heads[i].UnlockBits(data); }
            }
        }
        void AddKeys() {
            AddRow(244, new int[]{192,49,50,51,52,53,54,55,56,57,48,189,187,8}, new string[]{"`","1","2","3","4","5","6","7","8","9","0","-","=","←"});
            AddRow(259, new int[]{9,81,87,69,82,84,89,85,73,79,80,219,221,220}, new string[]{"Tab","Q","W","E","R","T","Y","U","I","O","P","[","]","\\"});
            AddRow(274, new int[]{20,65,83,68,70,71,72,74,75,76,186,222,13}, new string[]{"Caps","A","S","D","F","G","H","J","K","L",";","'","↵"});
            AddRow(289, new int[]{160,90,88,67,86,66,78,77,188,190,191,161}, new string[]{"Shift","Z","X","C","V","B","N","M",",",".","/","Shift"});
            keys.Add(new KeyCap(162,"Ctrl",48,304,28)); keys.Add(new KeyCap(164,"Alt",79,304,24)); keys.Add(new KeyCap(32,"",106,304,102));
            keys.Add(new KeyCap(165,"Alt",211,304,24)); keys.Add(new KeyCap(163,"Ctrl",238,304,27));
        }
        void AddRow(float y, int[] vks, string[] names) {
            float x=48, step=217f / vks.Length;
            for(int i=0;i<vks.Length;i++) keys.Add(new KeyCap(vks[i], names[i], x+i*step, y, step-2));
        }
        static GraphicsPath Round(RectangleF r, float rad) {
            var p=new GraphicsPath(); float d=rad*2;
            p.AddArc(r.X,r.Y,d,d,180,90); p.AddArc(r.Right-d,r.Y,d,d,270,90); p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90); p.AddArc(r.X,r.Bottom-d,d,d,90,90); p.CloseFigure(); return p;
        }
        static void Box(Graphics g, RectangleF r, Color fill, Color line, float rad) {
            using(var p=Round(r,rad)) using(var b=new SolidBrush(fill)) using(var pen=new Pen(line,1)) { g.FillPath(b,p); g.DrawPath(pen,p); }
        }
        static Bitmap MakeCoat(Bitmap source) {
            const int scale = 2;
            int width = CoatWidth * scale, height = CoatHeight * scale;
            var bounds = new Rectangle(0, 0, width, height);
            var texture = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(texture)) {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(source, new Rectangle(0, (49 - CoatY) * scale, width, 240 * scale));
            }

            // Widen the dark fabric at the mouse-side shoulder. Move the
            // existing panel edge so fur detail and color stay continuous.
            var fabric = new FurTexture(texture);
            var pixels = new int[width * height];
            for (int y = 0; y < height; y++) {
                float row = CoatY + y / (float)scale;
                float heightWeight = SmoothStep((row - 188) / 13) * (1 - SmoothStep((row - 207) / 23));
                for (int x = 0; x < width; x++) {
                    float column = CoatX + x / (float)scale;
                    float widthWeight = SmoothStep((column - 135) / 37) * (1 - SmoothStep((column - 178) / 37));
                    float offset = 16 * heightWeight * widthWeight * scale;
                    pixels[y * width + x] = fabric.Sample(x - offset, y);
                }
            }
            var data = texture.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try { Marshal.Copy(pixels, 0, data.Scan0, pixels.Length); }
            finally { texture.UnlockBits(data); }
            return texture;
        }
        static float SmoothStep(float value) {
            float t = Math.Max(0, Math.Min(1, value));
            return t * t * (3 - 2 * t);
        }
        public Bitmap Render(int width,int height,double time,bool sleep,bool paused,bool effects,float mx,float my,bool typing,int activeKey,bool leftButton,bool rightButton,bool wheel,Func<int,bool> held,bool preview,MotionFrame motion) {
            var output=new Bitmap(width,height,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(output)) {
                g.SmoothingMode=SmoothingMode.AntiAlias; g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                g.ScaleTransform(width/400f,height/328f);
                float rest=motion.Rest;
                float breath=motion.Bob*.65f+rest*7;
                PointF target = Plane(248,38); target.Y+=typing?1:-9;
                if(typing) foreach(var key in keys) if(key.Vk==activeKey) { target=KeyboardPoint(key.Rect.X+key.Rect.Width/2,key.Rect.Y+6); target.Y+=2; break; }
                PointF keyboardRest = videoModel ? Plane(179,32) : new PointF(235,251);
                keyboardRest.Y += motion.Breathing * .5f;
                if (!typing) target = Lerp(target, keyboardRest, rest);
                // The viewer's left paw uses the mouse; the right paw types.
                float mu=77+mx*19, mv=47+my*12;
                float press=leftButton||rightButton?2:0;
                PointF mouseTarget=Plane(mu,mv-4); mouseTarget.Y+=press;
                PointF mouseRest = videoModel ? Plane(193,27) : new PointF(160,239);
                mouseRest.Y += motion.Breathing * .5f;
                if (!leftButton && !rightButton) mouseTarget = Lerp(mouseTarget, mouseRest, rest);
                // Smooth the reach, rather than teleporting between distant keys.
                double frameTime = Math.Max(0, Math.Min(.1, time - lastFrame));
                float blend = lastFrame < 0 ? 1 : (float)(1-Math.Exp(-frameTime*25));
                PointF lp = preview?target:Lerp(leftTip,target,blend);
                PointF rp = preview?mouseTarget:Lerp(rightTip,mouseTarget,blend);
                // The dance reference shows the shoulder carrying the reach.
                // Move the coat with it while keeping the paw on the device.
                var neutralGrip = Plane(77,43);
                var leanTarget = new PointF(
                    (mouseTarget.X - neutralGrip.X) * .14f * (1 - rest),
                    (mouseTarget.Y - neutralGrip.Y) * .12f * (1 - rest));
                float leanBlend = lastFrame < 0 ? 1 : (float)(1 - Math.Exp(-frameTime * 34));
                PointF lean = preview ? leanTarget : Lerp(mouseLean, leanTarget, leanBlend);
                if(!preview) { leftTip=lp; rightTip=rp; mouseLean=lean; lastFrame=time; }
                float coatX = mx + lean.X;
                float coatY = breath + lean.Y;
                PointF ls = new PointF((videoModel ? 250 : 259) + coatX, (videoModel ? 207 + rest * 7 : 201) + coatY);
                PointF rs = new PointF((videoModel ? 171 : 164) + coatX, (videoModel ? 205 + rest * 6 : 197) + coatY);
                PointF le = new PointF(ls.X + (lp.X - ls.X) * .38f + 9, ls.Y + (lp.Y - ls.Y) * .56f);
                // Keep the upper sleeve close to the body's side before
                // curving out toward the mouse or down into the resting paw.
                PointF re = new PointF(
                    rs.X + (rp.X - rs.X) * .61f - 5 * (1 - rest),
                    rs.Y + (rp.Y - rs.Y) * .43f + 3);
                if (videoModel) {
                    le = Lerp(le, new PointF(267 + coatX, 254), rest);
                    re = Lerp(re, new PointF(150 + coatX, 250), rest);
                }
                // Body stays behind the desk; no pose-swapping of the whole character.
                var save=g.Save();
                // The torso artwork includes broad coat-side lobes. Tuck those
                // inside the shoulders so they cannot read as a second sleeve.
                using(var torso=new GraphicsPath()) {
                    if (videoModel) {
                        // The video torso already has an armless shoulder contour.
                        // Keep it behind the sleeve across the whole joint.
                        torso.AddLine(129 + coatX, 188 + coatY, 258 + coatX, 188 + coatY);
                        torso.AddBezier(258 + coatX, 188 + coatY, 270 + coatX, 208 + coatY, 269 + coatX, 239 + coatY, 263 + coatX, 264 + coatY);
                        torso.AddLine(263 + coatX, 264 + coatY, 143 + coatX, 264 + coatY);
                        torso.CloseFigure();
                    } else {
                        torso.AddBezier(151+coatX,188+coatY,170+coatX,185+coatY,245+coatX,186+coatY,264+coatX,193+coatY);
                        torso.AddLine(264+coatX,193+coatY,273+coatX,251+coatY);
                        torso.AddLine(273+coatX,251+coatY,275+coatX,263+coatY);
                        torso.AddLine(275+coatX,263+coatY,140+coatX,249+coatY);
                        torso.CloseFigure();
                    }
                    g.SetClip(torso);
                }
                // A short, broad upper torso tucked behind the desktop replaces
                // the upright full-belly pose. The head leans over the devices.
                g.DrawImage(coat,new RectangleF(CoatX+coatX,CoatY+coatY,CoatWidth,CoatHeight)); g.Restore(save);
                Surface(g,0,0,380,95,Color.FromArgb(243,46,53,63),Color.FromArgb(170,130,146,161),0);
                Surface(g,8,12,139,76,Color.FromArgb(255,68,79,93),Color.FromArgb(160,116,136,155),1);
                DrawKeyboard(g,typing,activeKey,paused,held);
                if(effects && !sleep) {
                    using(var pen=new Pen(Color.FromArgb(40,125,211,225),1)) g.DrawLine(pen,Plane(77,47),Plane(mu,mv));
                }
                DrawMouse(g,mu,mv,press,leftButton,rightButton,wheel);
                // Keep the original rounded flipper silhouettes. Bury the
                // mouse-side root beneath the collar instead of fading its
                // exposed fabric and letting the desktop show through.
                if (videoModel && rest > 0) {
                    DrawRestShadow(g, lp, rest);
                    DrawRestShadow(g, rp, rest);
                }
                Arm(g,leftFur,ls,le,lp,videoModel ? 35 + rest * 2 : 39,videoModel);
                Arm(g,rightFur,rs,re,rp,videoModel ? 35 + rest * 2 : 38,videoModel);
                // In a doze the cheek rests above the relaxed paws and desktop,
                // rather than getting clipped by the desk's rear edge.
                float drop=rest*(videoModel ? 31 : 27)+motion.Nod*(videoModel ? 3 : 6);
                save=g.Save();
                float pivotY=195+breath+drop;
                g.TranslateTransform(209+mx,pivotY); g.RotateTransform(motion.Tilt*.65f+1.5f);
                g.ScaleTransform(1+rest*.014f,1-rest*.035f-motion.Nod*.012f); g.TranslateTransform(-(209+mx),-pivotY);
                g.TranslateTransform(mx + lean.X * .6f, breath + drop + lean.Y * .6f);
                var headRect=new Rectangle(90,29,240,heads[0].Height);
                if(motion.FaceBlend<1) DrawHead(g,heads[motion.PreviousFace],headRect,1);
                DrawHead(g,heads[motion.Face],headRect,motion.FaceBlend);
                if(effects&&sleep&&!paused&&rest>.5f) {
                    float radius=4+(motion.Breathing+1)*2.1f;
                    var bubble=new RectangleF(220+mx-radius,174+breath+drop-radius,radius*2,radius*2);
                    using(var fill=new SolidBrush(Color.FromArgb((int)(rest*90),172,222,241))) using(var pen=new Pen(Color.FromArgb((int)(rest*130),218,243,250),.9f)) { g.FillEllipse(fill,bubble); g.DrawEllipse(pen,bubble); }
                    using(var shine=new SolidBrush(Color.FromArgb((int)(rest*170),255,255,255))) g.FillEllipse(shine,bubble.X+radius*.35f,bubble.Y+radius*.3f,radius*.5f,radius*.35f);
                }
                g.Restore(save);
                // Keep mouse buttons visible above the paw tip for click feedback.
                if(leftButton||rightButton) {
                    var p=Plane(mu,mv); using(var pen=new Pen(leftButton?Color.FromArgb(102,231,242):Color.FromArgb(255,197,142),1.5f)) g.DrawArc(pen,p.X-22,p.Y-13+press,44,30,0,180);
                }
                if(sleep&&effects&&!paused) for(int i=0;i<3;i++) {
                    float phase=(float)((time/3.2+i/3.0)%1),alpha=(float)Math.Sin(phase*Math.PI)*rest;
                    using(var font=new Font("Segoe UI",10+phase*6,FontStyle.Bold)) using(var brush=new SolidBrush(Color.FromArgb((int)(alpha*200),180,212,238))) g.DrawString("z",font,brush,290+phase*20,164+rest*15-phase*39);
                }
                if(paused) using(var font=new Font("Microsoft YaHei UI",9)) using(var brush=new SolidBrush(Color.FromArgb(230,217,230,239))) g.DrawString(Ui.Text("已暂停"),font,brush,294,300);
            }
            return output;
        }
        static void DrawRestShadow(Graphics g, PointF paw, float rest) {
            using (var outline = new GraphicsPath()) {
                outline.AddEllipse(paw.X - 17, paw.Y - 7, 34, 15);
                using (var shadow = new PathGradientBrush(outline)) {
                    shadow.CenterColor = Color.FromArgb((int)(100 * rest), 20, 17, 23);
                    shadow.SurroundColors = new[] { Color.FromArgb(0, 20, 17, 23) };
                    g.FillPath(shadow, outline);
                }
            }
        }
        // One affine desktop plane is shared by the pad, mouse, keyboard and
        // paw contact points. Device textures face the character (180 degrees).
        static PointF Plane(float u,float v) { return new PointF(38+.9f*u-.28f*v,214+.15f*u+.55f*v); }
        static PointF KeyboardPoint(float x,float y) { return Plane(366-(x-38)*220/240,92-(y-237)*84/90); }
        static void Surface(Graphics g,float u,float v,float w,float h,Color color,Color edge,float thickness) {
            var top=new PointF[]{Plane(u,v),Plane(u+w,v),Plane(u+w,v+h),Plane(u,v+h)};
            if(thickness>0) { var basePoints=(PointF[])top.Clone(); for(int i=0;i<4;i++) basePoints[i].Y+=thickness; using(var b=new SolidBrush(Color.FromArgb(255,35,43,53))) g.FillPolygon(b,basePoints); }
            using(var b=new SolidBrush(color)) using(var p=new Pen(edge,1)) { g.FillPolygon(b,top); g.DrawPolygon(p,top); }
        }
        void DrawKeyboard(Graphics g,bool typing,int activeKey,bool paused,Func<int,bool> held) {
            Surface(g,146,8,220,84,Color.FromArgb(255,104,121,134),Color.FromArgb(190,155,174,185),3);
            using(var keyboard=new Bitmap(240,90,PixelFormat.Format32bppPArgb)) {
                using(var kg=Graphics.FromImage(keyboard)) {
                    kg.SmoothingMode=SmoothingMode.AntiAlias; kg.TranslateTransform(-38,-237);
                    Box(kg,new RectangleF(38,237,240,90),Color.FromArgb(255,207,218,225),Color.FromArgb(255,146,163,177),5);
                    using(var font=new Font("Segoe UI",6.2f,FontStyle.Bold)) using(var text=new SolidBrush(Color.FromArgb(83,103,116))) using(var downText=new SolidBrush(Color.White)) {
                        foreach(var key in keys) {
                            bool down=!paused&&(held(key.Vk)||(typing&&key.Vk==activeKey)); var r=key.Rect;
                            if(down) r.Y+=1;
                            Box(kg,r,down?Color.FromArgb(46,170,189):Color.FromArgb(248,251,253),Color.FromArgb(163,181,194),2);
                            using(var fmt=new StringFormat {Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center}) kg.DrawString(key.Label,font,down?downText:text,r,fmt);
                        }
                    }
                }
                g.DrawImage(keyboard,new PointF[]{Plane(366,92),Plane(146,92),Plane(366,8)});
            }
        }
        static void DrawMouse(Graphics g,float u,float v,float press,bool left,bool right,bool wheel) {
            // Nose/buttons point away from the pet; its wrist rests on the back.
            var nose=Plane(u,v+32); using(var p=new Pen(Color.FromArgb(166,182,194),1.2f)) g.DrawBezier(p,nose,Plane(u+5,v+50),Plane(u+34,v+28),Plane(143,89));
            using(var image=new Bitmap(44,68,PixelFormat.Format32bppPArgb)) {
                using(var mg=Graphics.FromImage(image)) {
                    mg.SmoothingMode=SmoothingMode.AntiAlias;
                    Box(mg,new RectangleF(3,2+press,38,62-press),Color.FromArgb(244,249,251),Color.FromArgb(130,153,171),17);
                    Box(mg,new RectangleF(6,5+press,14,25),left?Color.FromArgb(61,188,205):Color.FromArgb(221,233,239),Color.FromArgb(151,177,194),5);
                    Box(mg,new RectangleF(24,5+press,14,25),right?Color.FromArgb(244,174,115):Color.FromArgb(221,233,239),Color.FromArgb(151,177,194),5);
                    using(var b=new SolidBrush(wheel?Color.FromArgb(247,184,93):Color.FromArgb(109,140,159))) mg.FillRectangle(b,20,9+press,4,14);
                }
                var points=new PointF[]{Plane(u+22,v+34),Plane(u-22,v+34),Plane(u+22,v-34)};
                for(int i=0;i<3;i++) points[i].Y+=press;
                g.DrawImage(image,points);
            }
        }
        static void DrawHead(Graphics g,Bitmap head,Rectangle rect,float alpha) {
            using(var attributes=new ImageAttributes()) {
                var matrix=new ColorMatrix(); matrix.Matrix33=alpha; attributes.SetColorMatrix(matrix);
                g.DrawImage(head,rect,0,0,head.Width,head.Height,GraphicsUnit.Pixel,attributes);
            }
        }
        public void Dispose() { coat.Dispose(); foreach(var head in heads) head.Dispose(); }
        static PointF Lerp(PointF a,PointF b,float t) { return new PointF(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t); }
        static PointF Curve(PointF a,PointF control,PointF b,float t) {
            float s=1-t; return new PointF(s*s*a.X+2*s*t*control.X+t*t*b.X,s*s*a.Y+2*s*t*control.Y+t*t*b.Y);
        }
        static PointF Normal(PointF a,PointF control,PointF b,float t) {
            float dx=2*((1-t)*(control.X-a.X)+t*(b.X-control.X));
            float dy=2*((1-t)*(control.Y-a.Y)+t*(b.Y-control.Y));
            float len=Math.Max(.001f,(float)Math.Sqrt(dx*dx+dy*dy)); return new PointF(dy/len,-dx/len);
        }
        static int ScalePixel(int pixel, float opacity) {
            int result = 0;
            for (int shift = 0; shift < 32; shift += 8)
                result |= (int)(((pixel >> shift) & 255) * opacity) << shift;
            return result;
        }
        static void Arm(Graphics g,FurTexture fur,PointF shoulder,PointF elbow,PointF tip,float width,bool videoModel) {
            // Rasterize shared-edge triangles, rather than independent GDI strips:
            // strip interpolation leaves dotted seams on the plush at deep bends.
            const int strips=28;
            PointF[] left=new PointF[strips+1],right=new PointF[strips+1];
            float minX=400,minY=328,maxX=0,maxY=0;
            for(int i=0;i<=strips;i++) {
                float t=i/(float)strips;
                PointF p=Curve(shoulder,elbow,tip,t),n=Normal(shoulder,elbow,tip,t); float half=width*(1-.12f*t)/2;
                left[i]=new PointF(p.X-n.X*half,p.Y-n.Y*half); right[i]=new PointF(p.X+n.X*half,p.Y+n.Y*half);
                minX=Math.Min(minX,Math.Min(left[i].X,right[i].X)); maxX=Math.Max(maxX,Math.Max(left[i].X,right[i].X));
                minY=Math.Min(minY,Math.Min(left[i].Y,right[i].Y)); maxY=Math.Max(maxY,Math.Max(left[i].Y,right[i].Y));
            }
            int ox=(int)Math.Floor(minX)-2,oy=(int)Math.Floor(minY)-2;
            int w=(int)Math.Ceiling(maxX)-ox+3,h=(int)Math.Ceiling(maxY)-oy+3;
            int[] pixels=new int[w*h];
            for(int i=0;i<strips;i++) {
                float v0=i/(float)strips,v1=(i+1)/(float)strips;
                PointF a=Offset(left[i],ox,oy),b=Offset(right[i],ox,oy),c=Offset(right[i+1],ox,oy),d=Offset(left[i+1],ox,oy);
                Triangle(pixels,w,h,fur,a,b,c,new PointF(0,v0),new PointF(1,v0),new PointF(1,v1),videoModel);
                Triangle(pixels,w,h,fur,a,c,d,new PointF(0,v0),new PointF(1,v1),new PointF(0,v1),videoModel);
            }
            using(var b=new Bitmap(w,h,PixelFormat.Format32bppPArgb)) {
                var data=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
                try { Marshal.Copy(pixels,0,data.Scan0,pixels.Length); } finally { b.UnlockBits(data); }
                g.DrawImage(b,new RectangleF(ox,oy,w,h));
            }
        }
        static PointF Offset(PointF p,int x,int y) { return new PointF(p.X-x,p.Y-y); }
        static void Triangle(int[] dst,int w,int h,FurTexture tex,PointF a,PointF b,PointF c,PointF ua,PointF ub,PointF uc,bool videoModel) {
            float det=(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X); if(Math.Abs(det)<.0001f) return;
            int x0=Math.Max(0,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X)))),x1=Math.Min(w-1,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))));
            int y0=Math.Max(0,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y)))),y1=Math.Min(h-1,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
            for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++) {
                float dx=x+.5f-a.X,dy=y+.5f-a.Y;
                float wb=(dx*(c.Y-a.Y)-dy*(c.X-a.X))/det,wc=((b.X-a.X)*dy-(b.Y-a.Y)*dx)/det,wa=1-wb-wc;
                if(wa<-.00001f||wb<-.00001f||wc<-.00001f) continue;
                float along = wa * ua.Y + wb * ub.Y + wc * uc.Y;
                float across = wa * ua.X + wb * ub.X + wc * uc.X;
                int pixel = tex.Sample(across * (tex.Width - 1), (videoModel ? .18f + .82f * along : along) * (tex.Height - 1));
                // Fade the mesh cut only inside the matte black shoulder.
                // Gently shade the root to reduce the baked light on the seam.
                if (videoModel && along < .38f) {
                    float join = SmoothStep(along / .38f);
                    int alpha = (int)((uint)pixel >> 24);
                    pixel = (ScalePixel(pixel, .77f + .23f * join) & 0x00ffffff) | (alpha << 24);
                    if (along < .065f) pixel = ScalePixel(pixel, SmoothStep(along / .065f));
                }
                dst[y*w+x] = pixel;
            }
        }
        sealed class FurTexture {
            public readonly int Width,Height; readonly int[] data;
            public FurTexture(Bitmap b) {
                Width=b.Width; Height=b.Height; data=new int[Width*Height];
                var locked=b.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
                try { Marshal.Copy(locked.Scan0,data,0,data.Length); } finally { b.UnlockBits(locked); }
            }
            public int Sample(float u,float v) {
                u=Math.Max(0,Math.Min(Width-1,u)); v=Math.Max(0,Math.Min(Height-1,v));
                int x=(int)u,y=(int)v,x1=Math.Min(x+1,Width-1),y1=Math.Min(y+1,Height-1);
                float fx=u-x,fy=v-y; int a=data[y*Width+x],b=data[y*Width+x1],c=data[y1*Width+x],d=data[y1*Width+x1];
                int result=0;
                for(int shift=0;shift<=24;shift+=8) {
                    float top=((a>>shift)&255)*(1-fx)+((b>>shift)&255)*fx;
                    float bottom=((c>>shift)&255)*(1-fx)+((d>>shift)&255)*fx;
                    result|=((int)(top*(1-fy)+bottom*fy))<<shift;
                }
                return result;
            }
        }
    }
}
