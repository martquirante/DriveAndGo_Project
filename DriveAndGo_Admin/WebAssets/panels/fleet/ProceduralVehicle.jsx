import React, { useRef } from 'react';
import { useFrame } from '@react-three/fiber';

/**
 * ProceduralVehicle: A high-fidelity, standalone 3D luxury sports sedan component 
 * rendered entirely using advanced React Three Fiber primitives and materials.
 */
export default function ProceduralVehicle({ isDriving = true, paintColor = "#e11d48", ...props }) {
  const vehicleRef = useRef();
  
  // Wheel refs for animating rotation during driving
  const flWheelRef = useRef();
  const frWheelRef = useRef();
  const blWheelRef = useRef();
  const brWheelRef = useRef();

  useFrame((state, delta) => {
    // Subtle engine vibration (micro-shaking)
    if (vehicleRef.current) {
      const time = state.clock.getElapsedTime();
      vehicleRef.current.position.y = (isDriving ? Math.sin(time * 22) * 0.012 : 0);
      vehicleRef.current.rotation.z = (isDriving ? Math.sin(time * 14) * 0.006 : 0);
    }

    if (isDriving) {
      const spinSpeed = 15; // Wheel roll speed in radians/sec
      
      // Rotate wheels around their local Y-axis (the cylinder's height axis)
      if (flWheelRef.current) flWheelRef.current.rotation.y += spinSpeed * delta;
      if (frWheelRef.current) frWheelRef.current.rotation.y += spinSpeed * delta;
      if (blWheelRef.current) blWheelRef.current.rotation.y += spinSpeed * delta;
      if (brWheelRef.current) brWheelRef.current.rotation.y += spinSpeed * delta;
    }
  });

  // 1. High Gloss Paint Material (Slate Pearl or Crimson Metallic)
  const bodyMaterialProps = {
    color: paintColor,
    metalness: 0.9,
    roughness: 0.15,
    clearcoat: 1.0,
    clearcoatRoughness: 0.1,
  };

  // 2. Translucent Glass Material
  const glassMaterialProps = {
    color: "#0f172a",
    transparent: true,
    opacity: 0.75,
    roughness: 0.05,
    metalness: 0.9,
  };

  // 3. High Gloss Chrome / Alloy Material
  const chromeMaterialProps = {
    color: "#cbd5e1",
    metalness: 1.0,
    roughness: 0.05,
  };

  // 4. Matte Tire Rubber Material
  const tireMaterialProps = {
    color: "#111115",
    roughness: 0.85,
    metalness: 0.1,
  };

  // 5. Volumetric Headlight Material
  const headlightMaterialProps = {
    color: "#fff9e6",
    emissive: "#fff1b8",
    emissiveIntensity: isDriving ? 3.0 : 1.0,
    roughness: 0.1,
  };

  // 6. Tail Light Material
  const taillightMaterialProps = {
    color: "#ff0000",
    emissive: "#ff0000",
    emissiveIntensity: 2.0,
  };

  // Wheel Assembly Helper
  const renderWheel = (ref, position) => {
    return (
      <group ref={ref} position={position} rotation={[0, 0, Math.PI / 2]}>
        {/* Tire rubber outer ring */}
        <mesh castShadow receiveShadow>
          <cylinderGeometry args={[0.45, 0.45, 0.28, 24]} />
          <meshStandardMaterial {...tireMaterialProps} />
        </mesh>
        
        {/* Chrome alloy rim outer edge */}
        <mesh>
          <cylinderGeometry args={[0.3, 0.3, 0.3, 16]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>

        {/* 5-Spoke Wheel Alloy Rims */}
        {[0, 1, 2, 3, 4].map((i) => {
          const angle = (i / 5) * Math.PI * 2;
          return (
            <mesh 
              key={i} 
              position={[Math.cos(angle) * 0.15, 0.01, Math.sin(angle) * 0.15]} 
              rotation={[0, angle, 0]}
            >
              <boxGeometry args={[0.22, 0.32, 0.04]} />
              <meshStandardMaterial {...chromeMaterialProps} />
            </mesh>
          );
        })}

        {/* Center hub cap */}
        <mesh position={[0, 0.16, 0]}>
          <cylinderGeometry args={[0.08, 0.08, 0.02, 12]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>
      </group>
    );
  };

  return (
    <>
      {/* Overhead Directional Light for premium paint clearcoat reflections */}
      <directionalLight 
        position={[0, 8, 2]} 
        intensity={2.0} 
        castShadow 
        shadow-mapSize={[1024, 1024]}
        shadow-bias={-0.0001}
      />
      
      <group ref={vehicleRef} {...props}>
        {/* 1. LOWER CHASSIS & UNDERBODY */}
        <mesh castShadow receiveShadow position={[0, 0.15, 0]}>
          <boxGeometry args={[1.88, 0.1, 4.3]} />
          <meshStandardMaterial color="#0b0f19" roughness={0.9} metalness={0.2} />
        </mesh>

        {/* Side Skirts */}
        <mesh position={[-0.92, 0.18, 0]} castShadow>
          <boxGeometry args={[0.06, 0.12, 3.0]} />
          <meshStandardMaterial color="#0b0f19" roughness={0.9} />
        </mesh>
        <mesh position={[0.92, 0.18, 0]} castShadow>
          <boxGeometry args={[0.06, 0.12, 3.0]} />
          <meshStandardMaterial color="#0b0f19" roughness={0.9} />
        </mesh>

        {/* 2. MAIN VEHICLE BODY PANEL ASSEMBLY */}
        {/* Front Nose & Bumper */}
        <mesh position={[0, 0.32, -1.95]} castShadow receiveShadow>
          <boxGeometry args={[1.82, 0.35, 0.4]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Front Grille Accent */}
        <mesh position={[0, 0.32, -2.16]}>
          <boxGeometry args={[1.0, 0.15, 0.02]} />
          <meshStandardMaterial color="#090d16" roughness={0.6} metalness={0.8} />
        </mesh>
        <mesh position={[0, 0.32, -2.17]}>
          <boxGeometry args={[0.95, 0.03, 0.02]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>

        {/* Engine Hood */}
        <mesh position={[0, 0.44, -1.2]} castShadow receiveShadow>
          <boxGeometry args={[1.8, 0.15, 1.4]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Center Console / Cockpit Lower Body */}
        <mesh position={[0, 0.42, 0.1]} castShadow receiveShadow>
          <boxGeometry args={[1.84, 0.45, 1.6]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Rear Quarter Panels & Trunk */}
        <mesh position={[0, 0.48, 1.3]} castShadow receiveShadow>
          <boxGeometry args={[1.82, 0.45, 1.2]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Rear Bumper Area */}
        <mesh position={[0, 0.35, 2.0]} castShadow receiveShadow>
          <boxGeometry args={[1.82, 0.4, 0.3]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Rear Spoiler / Lip Wing */}
        <group position={[0, 0.72, 1.85]}>
          {/* Left winglet support */}
          <mesh position={[-0.8, -0.06, 0]}>
            <boxGeometry args={[0.04, 0.12, 0.15]} />
            <meshStandardMaterial {...bodyMaterialProps} />
          </mesh>
          {/* Right winglet support */}
          <mesh position={[0.8, -0.06, 0]}>
            <boxGeometry args={[0.04, 0.12, 0.15]} />
            <meshStandardMaterial {...bodyMaterialProps} />
          </mesh>
          {/* Main Spoiler blade */}
          <mesh castShadow rotation={[0.05, 0, 0]}>
            <boxGeometry args={[1.8, 0.03, 0.28]} />
            <meshStandardMaterial color="#090d16" roughness={0.1} metalness={0.9} />
          </mesh>
        </group>

        {/* 3. GLASS CABIN ASSEMBLY */}
        {/* Front Windshield */}
        <mesh position={[0, 0.74, -0.58]} rotation={[-0.78, 0, 0]} castShadow>
          <boxGeometry args={[1.5, 0.02, 0.95]} />
          <meshStandardMaterial {...glassMaterialProps} />
        </mesh>

        {/* Cabin Roof Cover */}
        <mesh position={[0, 0.94, 0.15]} castShadow>
          <boxGeometry args={[1.44, 0.04, 1.25]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Rear Window Glass */}
        <mesh position={[0, 0.78, 0.92]} rotation={[0.7, 0, 0]} castShadow>
          <boxGeometry args={[1.45, 0.02, 0.95]} />
          <meshStandardMaterial {...glassMaterialProps} />
        </mesh>

        {/* Side Glass Panels */}
        <mesh position={[-0.89, 0.7, 0.15]} castShadow>
          <boxGeometry args={[0.01, 0.44, 1.2]} />
          <meshStandardMaterial {...glassMaterialProps} />
        </mesh>
        <mesh position={[0.89, 0.7, 0.15]} castShadow>
          <boxGeometry args={[0.01, 0.44, 1.2]} />
          <meshStandardMaterial {...glassMaterialProps} />
        </mesh>

        {/* A-Pillars & C-Pillars (Chrome and Paint trims for definition) */}
        <mesh position={[-0.89, 0.74, -0.58]} rotation={[-0.78, 0, 0]}>
          <boxGeometry args={[0.03, 0.08, 0.95]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>
        <mesh position={[0.89, 0.74, -0.58]} rotation={[-0.78, 0, 0]}>
          <boxGeometry args={[0.03, 0.08, 0.95]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>
        <mesh position={[-0.89, 0.78, 0.92]} rotation={[0.7, 0, 0]}>
          <boxGeometry args={[0.03, 0.08, 0.95]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>
        <mesh position={[0.89, 0.78, 0.92]} rotation={[0.7, 0, 0]}>
          <boxGeometry args={[0.03, 0.08, 0.95]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>

        {/* Side Mirrors */}
        <mesh position={[-0.96, 0.58, -0.5]} castShadow>
          <boxGeometry args={[0.15, 0.1, 0.18]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>
        <mesh position={[0.96, 0.58, -0.5]} castShadow>
          <boxGeometry args={[0.15, 0.1, 0.18]} />
          <meshStandardMaterial {...bodyMaterialProps} />
        </mesh>
        {/* Side Mirror Reflective Part */}
        <mesh position={[-1.04, 0.58, -0.5]} rotation={[0, Math.PI / 2, 0]}>
          <boxGeometry args={[0.14, 0.08, 0.01]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>
        <mesh position={[1.04, 0.58, -0.5]} rotation={[0, -Math.PI / 2, 0]}>
          <boxGeometry args={[0.14, 0.08, 0.01]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>

        {/* 4. LIGHTS (HEADLIGHTS & TAILLIGHTS) */}
        {/* Front Left Headlight */}
        <mesh position={[-0.68, 0.32, -2.13]}>
          <boxGeometry args={[0.26, 0.08, 0.05]} />
          <meshStandardMaterial {...headlightMaterialProps} />
        </mesh>
        {/* Front Right Headlight */}
        <mesh position={[0.68, 0.32, -2.13]}>
          <boxGeometry args={[0.26, 0.08, 0.05]} />
          <meshStandardMaterial {...headlightMaterialProps} />
        </mesh>

        {/* Front Headlight Light Sources */}
        <spotLight 
          position={[-0.68, 0.32, -2.15]} 
          target-position={[-0.68, 0.32, -10]} 
          intensity={6} 
          angle={Math.PI / 6} 
          penumbra={0.4} 
          castShadow 
        />
        <spotLight 
          position={[0.68, 0.32, -2.15]} 
          target-position={[0.68, 0.32, -10]} 
          intensity={6} 
          angle={Math.PI / 6} 
          penumbra={0.4} 
          castShadow 
        />

        {/* Rear Left Tail Light */}
        <mesh position={[-0.68, 0.44, 2.13]}>
          <boxGeometry args={[0.3, 0.08, 0.04]} />
          <meshStandardMaterial {...taillightMaterialProps} />
        </mesh>
        {/* Rear Right Tail Light */}
        <mesh position={[0.68, 0.44, 2.13]}>
          <boxGeometry args={[0.3, 0.08, 0.04]} />
          <meshStandardMaterial {...taillightMaterialProps} />
        </mesh>

        {/* Exhaust Pipes (Chrome accents at rear bottom) */}
        <mesh position={[-0.55, 0.16, 2.12]} rotation={[Math.PI / 2, 0, 0]}>
          <cylinderGeometry args={[0.07, 0.07, 0.18, 12]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>
        <mesh position={[0.55, 0.16, 2.12]} rotation={[Math.PI / 2, 0, 0]}>
          <cylinderGeometry args={[0.07, 0.07, 0.18, 12]} />
          <meshStandardMaterial {...chromeMaterialProps} />
        </mesh>

        {/* 5. WHEEL ASSEMBLIES */}
        {renderWheel(flWheelRef, [-0.96, 0.25, -1.3])}
        {renderWheel(frWheelRef, [0.96, 0.25, -1.3])}
        {renderWheel(blWheelRef, [-0.96, 0.25, 1.3])}
        {renderWheel(brWheelRef, [0.96, 0.25, 1.3])}
      </group>
    </>
  );
}
