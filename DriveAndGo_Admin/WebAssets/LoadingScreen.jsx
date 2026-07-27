import React, { useState, useEffect, useRef } from 'react';
import { Canvas, useFrame } from '@react-three/fiber';
import { OrbitControls, useGLTF, PerspectiveCamera } from '@react-three/drei';
import * as THREE from 'three';
import ProceduralVehicle from './ProceduralVehicle';


/**
 * Animated Grid Floor
 */
function ReflectiveGrid({ isDriving }) {
  const gridRef = useRef();

  useFrame((state) => {
    if (isDriving && gridRef.current) {
      const time = state.clock.getElapsedTime();
      gridRef.current.position.z = (time * 2.8) % 4; // infinite grid offset motion
    }
  });

  return (
    <gridHelper
      ref={gridRef}
      args={[80, 40, '#ff5a1f', '#222338']}
      position={[0, -0.8, 0]}
    />
  );
}

/**
 * LoadingScreen Main Component
 */
export default function LoadingScreen({ onComplete }) {
  const [loadingText, setLoadingText] = useState("Initializing secure shell...");
  const [isDriving, setIsDriving] = useState(true);
  const [garageTransition, setGarageTransition] = useState(false);

  useEffect(() => {
    // 1. Text cycle sequence
    const sequences = [
      { text: "Initializing secure shell...", delay: 0 },
      { text: "Synchronizing fleet metrics...", delay: 500 },
      { text: "Resolving active routing algorithms...", delay: 1000 },
      { text: "Welcome back, Admin.", delay: 1400 }
    ];

    sequences.forEach((seq) => {
      setTimeout(() => {
        setLoadingText(seq.text);
      }, seq.delay);
    });

    // 2. Trigger garage transition overlay near the end of 1500ms duration
    const transitionTimer = setTimeout(() => {
      setGarageTransition(true);
    }, 1300);

    // 3. Minimum timeout to allow driving animation to resolve before calling routing callback
    const completeTimer = setTimeout(() => {
      setIsDriving(false);
      if (onComplete) onComplete();
    }, 1600); // 1.6 seconds total minimum duration

    return () => {
      clearTimeout(transitionTimer);
      clearTimeout(completeTimer);
    };
  }, [onComplete]);

  return (
    <div className="fixed inset-0 z-[9999] flex flex-col items-center justify-center bg-[#05050b] text-slate-100 overflow-hidden font-sans">
      
      {/* Cinematic 3D Viewport */}
      <div className="relative w-full h-[65vh] flex items-center justify-center">
        
        {/* Glow ambient background lights */}
        <div className="absolute top-1/4 left-1/2 -translate-x-1/2 w-[500px] h-[250px] bg-orange-500/10 rounded-full blur-[120px] pointer-events-none"></div>
        
        <Canvas shadows>
          <PerspectiveCamera makeDefault position={[-5, 2.5, 9]} fov={40} />
          
          <ambientLight intensity={0.4} />
          <directionalLight 
            position={[5, 12, 5]} 
            intensity={1.2} 
            castShadow 
            shadow-mapSize={[1024, 1024]} 
          />
          <pointLight position={[-6, 4, -4]} color="#ff5a1f" intensity={2} />
          <pointLight position={[6, 3, 2]} color="#ffaa66" intensity={1.5} />

          {/* Sleek sports car */}
          <ProceduralVehicle isDriving={isDriving} />

          {/* reflective dark grid floor */}
          <ReflectiveGrid isDriving={isDriving} />

          {/* Camera controls */}
          <OrbitControls 
            enableZoom={false} 
            enablePan={false} 
            maxPolarAngle={Math.PI / 2.1} 
            minPolarAngle={Math.PI / 3}
          />
        </Canvas>

        {/* Ambient Overlay Vignette */}
        <div className="absolute inset-0 bg-gradient-to-t from-[#05050b] via-transparent to-[#05050b] pointer-events-none"></div>
      </div>

      {/* Progress & Telemetry Text Tracker */}
      <div className="flex flex-col items-center gap-4 mt-4 px-6 text-center max-w-md">
        
        {/* Modern circular pulsing spinner */}
        <div className="relative flex h-8 w-8 mb-2">
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-orange-500/30 opacity-75"></span>
          <span className="relative inline-flex rounded-full h-8 w-8 border-2 border-orange-500/20 border-t-orange-500 animate-spin"></span>
        </div>

        {/* Dynamic cycling loading steps */}
        <p className="text-sm font-semibold tracking-wider bg-gradient-to-r from-orange-400 via-amber-200 to-orange-500 bg-clip-text text-transparent transition-all duration-300">
          {loadingText}
        </p>

        {/* Sub-progress line indicator */}
        <div className="w-48 h-1 bg-slate-900 rounded-full overflow-hidden border border-white/5">
          <div className="h-full bg-gradient-to-r from-orange-600 to-amber-500 rounded-full animate-loaderWidth"></div>
        </div>
      </div>

      {/* Cinematic Garage Door Shadow Overlay Transition */}
      <div 
        className={`absolute inset-0 bg-black pointer-events-none transition-opacity duration-500 ease-in-out ${
          garageTransition ? 'opacity-100' : 'opacity-0'
        }`}
      />

    </div>
  );
}
