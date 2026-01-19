import React from 'react';
import { View } from 'react-native';
import { colors } from '../../theme';

interface BracketConnectorProps {
  fromX: number; // X position of source match (right edge)
  fromY: number; // Y position of source match (center)
  toX: number; // X position of destination match (left edge)
  toY: number; // Y position of destination match (center)
  isHighlighted?: boolean; // True if part of selected team's path
  isLoserPath?: boolean; // True if this is a loser dropping to losers bracket
}

// Renders a dashed line segment (horizontal or vertical)
const DashedLine: React.FC<{
  x: number;
  y: number;
  length: number;
  thickness: number;
  color: string;
  horizontal: boolean;
}> = ({ x, y, length, thickness, color, horizontal }) => {
  const dashLength = 5;
  const gapLength = 5;
  const segmentLength = dashLength + gapLength;
  const numSegments = Math.ceil(length / segmentLength);

  const dashes = [];
  for (let i = 0; i < numSegments; i++) {
    const offset = i * segmentLength;
    const remainingLength = length - offset;
    const thisDashLength = Math.min(dashLength, remainingLength);

    if (thisDashLength <= 0) break;

    dashes.push(
      <View
        key={i}
        pointerEvents="none"
        style={{
          position: 'absolute',
          left: horizontal ? x + offset : x - thickness / 2,
          top: horizontal ? y - thickness / 2 : y + offset,
          width: horizontal ? thisDashLength : thickness,
          height: horizontal ? thickness : thisDashLength,
          backgroundColor: color,
        }}
      />
    );
  }

  return <>{dashes}</>;
};

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

  // Determine stroke color
  let strokeColor: string = colors.border.muted;
  if (isLoserPath) {
    strokeColor = colors.status.error;
  } else if (isHighlighted) {
    strokeColor = colors.primary.teal;
  }

  // Determine stroke width
  const strokeWidth = isHighlighted ? 3 : 2;

  // Calculate dimensions
  const horizontalLength1 = midX - fromX;
  const horizontalLength2 = toX - midX;
  const verticalLength = Math.abs(toY - fromY);
  const goingDown = toY > fromY;
  const verticalTop = goingDown ? fromY : toY;

  if (isLoserPath) {
    // Render dashed lines for loser path
    return (
      <>
        {/* Horizontal line from source to midpoint */}
        <DashedLine
          x={fromX}
          y={fromY}
          length={horizontalLength1}
          thickness={strokeWidth}
          color={strokeColor}
          horizontal={true}
        />
        {/* Vertical line at midpoint */}
        {verticalLength > 0 && (
          <DashedLine
            x={midX}
            y={verticalTop}
            length={verticalLength}
            thickness={strokeWidth}
            color={strokeColor}
            horizontal={false}
          />
        )}
        {/* Horizontal line from midpoint to destination */}
        <DashedLine
          x={midX}
          y={toY}
          length={horizontalLength2}
          thickness={strokeWidth}
          color={strokeColor}
          horizontal={true}
        />
      </>
    );
  }

  // Render solid lines for normal paths
  return (
    <>
      {/* Horizontal line from source to midpoint */}
      <View
        pointerEvents="none"
        style={{
          position: 'absolute',
          left: fromX,
          top: fromY - strokeWidth / 2,
          width: horizontalLength1,
          height: strokeWidth,
          backgroundColor: strokeColor,
        }}
      />
      {/* Vertical line at midpoint */}
      {verticalLength > 0 && (
        <View
          pointerEvents="none"
          style={{
            position: 'absolute',
            left: midX - strokeWidth / 2,
            top: verticalTop,
            width: strokeWidth,
            height: verticalLength,
            backgroundColor: strokeColor,
          }}
        />
      )}
      {/* Horizontal line from midpoint to destination */}
      <View
        pointerEvents="none"
        style={{
          position: 'absolute',
          left: midX,
          top: toY - strokeWidth / 2,
          width: horizontalLength2,
          height: strokeWidth,
          backgroundColor: strokeColor,
        }}
      />
    </>
  );
};

export default BracketConnector;
