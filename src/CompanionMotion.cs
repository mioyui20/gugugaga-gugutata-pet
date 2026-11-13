using System;

namespace PenguinPet {
    struct MotionFrame {
        public int Face, PreviousFace;
        public float FaceBlend, Tilt, Bob, Rest, Nod, Breathing, Wake;
    }
    // Small, time-based motions leave the input-driven paws independent.
    sealed class CompanionMotion {
        int face, previous;
        double changed, nextBlink=3.7, blinkEnd, nextGlance=8.5;
        double happyUntil, focusUntil, wakeUntil, lastActivity;
        bool wasSleeping;
        float tilt, bob, rest;
        double last=-1;
        readonly Random random=new Random(6183);
        public void Key(double now) { Activity(now); focusUntil=now+1.1; }
        public void Click(double now) { Activity(now); }
        public void Pet(double now) { Activity(now); happyUntil=now+1.9; }
        public void Doze(double now) { lastActivity=now-46; happyUntil=focusUntil=wakeUntil=0; wasSleeping=true; }
        public void Activity(double now) { if(wasSleeping) { wakeUntil=now+.75; wasSleeping=false; } lastActivity=now; }
        public void Reset(double now) { lastActivity=now; happyUntil=focusUntil=wakeUntil=0; wasSleeping=false; }
        public MotionFrame Sample(double now,float gaze,bool paused,bool dragging) {
            double dt=last<0?.016:Math.Max(0,Math.Min(.1,now-last)); last=now;
            bool sleeping=!paused&&now-lastActivity>45;
            if(now>=nextBlink) { blinkEnd=now+.13; nextBlink=now+3.8+random.NextDouble()*3.2; }
            if(!paused&&!sleeping&&now>=nextGlance&&now>focusUntil) { happyUntil=now+.85; nextGlance=now+15+random.NextDouble()*12; }
            int requested=paused?0:sleeping?4:now<wakeUntil?5:now<happyUntil?2:now<blinkEnd?1:now<focusUntil?3:0;
            if(requested!=face) { previous=face; face=requested; changed=now; }
            double duration=face==1||previous==1?.045:face==4||previous==4?.3:.14;
            float blend=(float)Math.Min(1,Math.Max(0,(now-changed)/duration)); blend=blend*blend*(3-2*blend);
            float restEase=(float)(1-Math.Exp(-dt*(sleeping?1.9:9)));
            rest+=((sleeping?1:0)-rest)*restEase;
            double age=Math.Max(0,now-lastActivity-45),phase=age%8.6;
            // Slowly lose the fight against sleep, then catch the head in a
            // shorter rebound. A long quiet interval keeps it from bobbing nonstop.
            float nod=sleeping?(phase<1.5?Smooth((float)(phase/1.5)):phase<2.05?1-Smooth((float)((phase-1.5)/.55)):0):0;
            float breathing=(float)Math.Sin(now*2*Math.PI/3.6);
            float waking=now<wakeUntil?(float)Math.Sin(Math.PI*(1-(wakeUntil-now)/.75)):0;
            float targetTilt=paused?0:sleeping?6.5f+nod*3+breathing*.45f:dragging?(float)Math.Sin(now*7)*2:gaze*1.7f+(now<happyUntil?(float)Math.Sin(now*3.7)*1.5f:0)-waking*2.5f;
            float targetBob=paused?0:sleeping?breathing*1.35f:(float)Math.Sin(now*2.1)*.65f-waking*2;
            if(!paused&&now<happyUntil) targetBob-=(float)Math.Pow(Math.Sin((happyUntil-now)*4),2)*1.4f;
            float ease=(float)(1-Math.Exp(-dt*9)); tilt+=(targetTilt-tilt)*ease; bob+=(targetBob-bob)*ease;
            wasSleeping=sleeping;
            return new MotionFrame { Face=face,PreviousFace=previous,FaceBlend=blend,Tilt=tilt,Bob=bob,Rest=rest,Nod=nod*rest,Breathing=breathing*rest,Wake=waking };
        }
        static float Smooth(float t) { t=Math.Max(0,Math.Min(1,t)); return t*t*(3-2*t); }
        public static MotionFrame Preview(int expression) { return new MotionFrame { Face=expression,PreviousFace=expression,FaceBlend=1,Tilt=expression==4?6.5f:0,Bob=0,Rest=expression==4?1:0 }; }
    }
}
