import React from 'react';
import { Svg, Path } from 'react-native-svg';
import { colors } from '../../theme';

interface BracketConnectorProps {
  fromX: number; // X position of source match (right edge)
  fromY: number; // Y position of source match (center)
  toX: number; // X position of destination match (left edge)
  toY: number; // Y position of destination match (center)
  isHighlighted?: boolean; // True if part of selected team's path
  isLoserPath?: boolean; // True if this is a loser dropping to losers bracket
}

export const BracketConnector: React.FC<BracketConnectorProps> = ({
  fromX,
  fromY,
  toX,
  toY,
  isHighlighted = false,
  isLoserPath = false,
}) => {
  // Calculate the midpoint X for the step connector
  const midX = fromX + (toX - fromX) / 2;

  // Create the step-shaped path: horizontal -> vertical -> horizontal
  const pathData = `
    M ${fromX} ${fromY}
    L ${midX} ${fromY}
    L ${midX} ${toY}
    L ${toX} ${toY}
  `;

  // Determine stroke color
  let strokeColor: string = colors.border.muted; // Default
  if (isLoserPath) {
    strokeColor = colors.status.error;
  } else if (isHighlighted) {
    strokeColor = colors.primary.teal;
  }

  // Determine stroke width
  const strokeWidth = isHighlighted ? 3 : 2;

  // Calculate SVG viewBox to contain the path
  const minX = Math.min(fromX, toX);
  const maxX = Math.max(fromX, toX);
  const minY = Math.min(fromY, toY);
  const maxY = Math.max(fromY, toY);
  const padding = 5;

  const viewBox = `${minX - padding} ${minY - padding} ${
    maxX - minX + padding * 2
  } ${maxY - minY + padding * 2}`;

  return (
    <Svg
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        pointerEvents: 'none',
      }}
      viewBox={viewBox}
      preserveAspectRatio="none"
    >
      <Path
        d={pathData}
        stroke={strokeColor}
        strokeWidth={strokeWidth}
        fill="none"
        strokeDasharray={isLoserPath ? '5,5' : undefined}
      />
    </Svg>
  );
};

export default BracketConnector;
