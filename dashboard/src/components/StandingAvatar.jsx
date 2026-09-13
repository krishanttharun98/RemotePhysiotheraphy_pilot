import { useMemo } from 'react'

const SKIN = '#e8c7a8'
const SUIT = '#7dd3fc'
const DARK = '#38bdf8'

export default function StandingAvatar({ position = [0, 0, 0] }) {
  const joints = useMemo(
    () => ({
      head: [0, 1.58, 0],
      torso: [0, 1.12, 0],
      pelvis: [0, 0.78, 0],
      leftArm: [-0.24, 1.18, 0],
      rightArm: [0.24, 1.18, 0],
      leftLeg: [-0.1, 0.4, 0],
      rightLeg: [0.1, 0.4, 0],
    }),
    [],
  )

  return (
    <group position={position}>
      <mesh position={joints.head} castShadow>
        <sphereGeometry args={[0.11, 24, 24]} />
        <meshStandardMaterial color={SKIN} roughness={0.45} />
      </mesh>
      <mesh position={[0, 1.48, 0.09]}>
        <boxGeometry args={[0.08, 0.03, 0.03]} />
        <meshStandardMaterial color="#36a2c7" />
      </mesh>
      <mesh position={joints.torso} castShadow>
        <cylinderGeometry args={[0.16, 0.18, 0.52, 16]} />
        <meshStandardMaterial color={SUIT} roughness={0.55} />
      </mesh>
      <mesh position={joints.pelvis} castShadow>
        <cylinderGeometry args={[0.15, 0.14, 0.18, 16]} />
        <meshStandardMaterial color={DARK} roughness={0.6} />
      </mesh>
      <mesh position={joints.leftArm} rotation={[0, 0, 0.08]} castShadow>
        <cylinderGeometry args={[0.04, 0.045, 0.55, 12]} />
        <meshStandardMaterial color={SUIT} />
      </mesh>
      <mesh position={joints.rightArm} rotation={[0, 0, -0.08]} castShadow>
        <cylinderGeometry args={[0.04, 0.045, 0.55, 12]} />
        <meshStandardMaterial color={SUIT} />
      </mesh>
      <mesh position={[-0.24, 0.86, 0]}>
        <sphereGeometry args={[0.045, 12, 12]} />
        <meshStandardMaterial color={SKIN} />
      </mesh>
      <mesh position={[0.24, 0.86, 0]}>
        <sphereGeometry args={[0.045, 12, 12]} />
        <meshStandardMaterial color={SKIN} />
      </mesh>
      <mesh position={joints.leftLeg} castShadow>
        <cylinderGeometry args={[0.055, 0.065, 0.7, 12]} />
        <meshStandardMaterial color="#36a2c7" />
      </mesh>
      <mesh position={joints.rightLeg} castShadow>
        <cylinderGeometry args={[0.055, 0.065, 0.7, 12]} />
        <meshStandardMaterial color="#36a2c7" />
      </mesh>
      <mesh position={[-0.1, 0.04, 0.03]} rotation={[0.12, 0, 0]}>
        <boxGeometry args={[0.1, 0.05, 0.2]} />
        <meshStandardMaterial color="#1e293b" />
      </mesh>
      <mesh position={[0.1, 0.04, 0.03]} rotation={[0.12, 0, 0]}>
        <boxGeometry args={[0.1, 0.05, 0.2]} />
        <meshStandardMaterial color="#1e293b" />
      </mesh>
    </group>
  )
}

export const AVATAR_HEAD = [0, 1.58, 0]
