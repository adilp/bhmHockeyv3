import React, { forwardRef, useMemo } from 'react';
import { View, Text, Image, StyleSheet } from 'react-native';
import type { EventDto, EventRegistrationDto } from '@bhmhockey/shared';
import { colors, spacing, radius } from '../../theme';

interface RosterShareCardProps {
  event: EventDto;
  registrations: EventRegistrationDto[];
}

export const RosterShareCard = forwardRef<View, RosterShareCardProps>(
  ({ event, registrations }, ref) => {
    const { blackGoalies, whiteGoalies, blackSkaters, whiteSkaters } = useMemo(() => {
      const bGoalies = registrations
        .filter(r => r.teamAssignment === 'Black' && r.registeredPosition === 'Goalie')
        .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
      const wGoalies = registrations
        .filter(r => r.teamAssignment === 'White' && r.registeredPosition === 'Goalie')
        .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
      const bSkaters = registrations
        .filter(r => r.teamAssignment === 'Black' && r.registeredPosition !== 'Goalie')
        .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
      const wSkaters = registrations
        .filter(r => r.teamAssignment === 'White' && r.registeredPosition !== 'Goalie')
        .sort((a, b) => (a.rosterOrder ?? 999) - (b.rosterOrder ?? 999));
      return {
        blackGoalies: bGoalies,
        whiteGoalies: wGoalies,
        blackSkaters: bSkaters,
        whiteSkaters: wSkaters,
      };
    }, [registrations]);

    const eventDate = new Date(event.eventDate);
    const dateStr = eventDate.toLocaleDateString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
    });
    const timeStr = eventDate.toLocaleTimeString('en-US', {
      hour: 'numeric',
      minute: '2-digit',
    });
    const title = event.name ?? event.organizationName ?? 'Pickup Hockey';

    const goalieRowCount = Math.max(blackGoalies.length, whiteGoalies.length, 1);
    const skaterRowCount = Math.max(blackSkaters.length, whiteSkaters.length);

    return (
      <View ref={ref} collapsable={false} style={styles.card}>
        {/* Header */}
        <View style={styles.header}>
          <View style={styles.headerRow}>
            <Image
              source={require('../../assets/icon.png')}
              style={styles.appIcon}
            />
            <Text allowFontScaling={false} style={styles.title} numberOfLines={1}>
              {title}
            </Text>
          </View>
          <Text allowFontScaling={false} style={styles.dateLine}>
            {dateStr} {'\u00B7'} {timeStr}
          </Text>
        </View>

        {/* Team Headers */}
        <View style={styles.teamHeaderRow}>
          <View style={styles.teamHeaderLeft}>
            <View style={styles.blackCircle} />
            <Text allowFontScaling={false} style={styles.teamHeaderText}>
              BLACK
            </Text>
          </View>
          <View style={styles.teamHeaderRight}>
            <Text allowFontScaling={false} style={styles.teamHeaderText}>
              WHITE
            </Text>
            <View style={styles.whiteCircle} />
          </View>
        </View>

        {/* Goalie Section */}
        <View style={styles.goalieSection}>
          {Array.from({ length: goalieRowCount }).map((_, i) => {
            const blackGoalie = blackGoalies[i];
            const whiteGoalie = whiteGoalies[i];
            return (
              <View key={`goalie-${i}`} style={styles.playerRow}>
                <View style={styles.leftColumn}>
                  <Text allowFontScaling={false} style={styles.positionLabel}>
                    G:{' '}
                  </Text>
                  <Text
                    allowFontScaling={false}
                    style={styles.playerName}
                    numberOfLines={1}
                  >
                    {blackGoalie
                      ? `${blackGoalie.user.firstName} ${blackGoalie.user.lastName}`
                      : '\u2014'}
                  </Text>
                </View>
                <View style={styles.rightColumn}>
                  <Text allowFontScaling={false} style={styles.positionLabel}>
                    G:{' '}
                  </Text>
                  <Text
                    allowFontScaling={false}
                    style={styles.playerName}
                    numberOfLines={1}
                  >
                    {whiteGoalie
                      ? `${whiteGoalie.user.firstName} ${whiteGoalie.user.lastName}`
                      : '\u2014'}
                  </Text>
                </View>
              </View>
            );
          })}
        </View>

        {/* Separator */}
        <View style={styles.separator} />

        {/* Skaters Section */}
        <View style={styles.skatersSection}>
          {Array.from({ length: skaterRowCount }).map((_, i) => {
            const blackSkater = blackSkaters[i];
            const whiteSkater = whiteSkaters[i];
            const posLabel = event.slotPositionLabels?.[i + 1] ?? `${i + 1}`;
            return (
              <View key={`skater-${i}`} style={styles.playerRow}>
                <View style={styles.leftColumn}>
                  {blackSkater ? (
                    <>
                      <Text
                        allowFontScaling={false}
                        style={styles.positionLabel}
                      >
                        {posLabel}{' '}
                      </Text>
                      <Text
                        allowFontScaling={false}
                        style={styles.playerName}
                        numberOfLines={1}
                      >
                        {blackSkater.user.firstName} {blackSkater.user.lastName}
                      </Text>
                    </>
                  ) : (
                    <Text allowFontScaling={false} style={styles.playerName} />
                  )}
                </View>
                <View style={styles.rightColumn}>
                  {whiteSkater ? (
                    <>
                      <Text
                        allowFontScaling={false}
                        style={styles.positionLabel}
                      >
                        {posLabel}{' '}
                      </Text>
                      <Text
                        allowFontScaling={false}
                        style={styles.playerName}
                        numberOfLines={1}
                      >
                        {whiteSkater.user.firstName} {whiteSkater.user.lastName}
                      </Text>
                    </>
                  ) : (
                    <Text allowFontScaling={false} style={styles.playerName} />
                  )}
                </View>
              </View>
            );
          })}
        </View>

        {/* Footer */}
        <View style={styles.footer}>
          <Text allowFontScaling={false} style={styles.footerBrand}>
            BHM Hockey
          </Text>
          <Text allowFontScaling={false} style={styles.footerSubtext}>
            Available on the App Store & Google Play
          </Text>
        </View>
      </View>
    );
  },
);

RosterShareCard.displayName = 'RosterShareCard';

const styles = StyleSheet.create({
  card: {
    width: 390,
    backgroundColor: colors.bg.darkest,
    borderRadius: radius.xl,
    padding: spacing.md,
    overflow: 'hidden',
  },

  // Header
  header: {
    marginBottom: spacing.md,
  },
  headerRow: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: spacing.xs,
  },
  appIcon: {
    width: 32,
    height: 32,
    borderRadius: radius.sm, // closest token to desired ~6px rounding
    marginRight: spacing.sm,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
    color: colors.text.primary,
    flex: 1,
  },
  dateLine: {
    fontSize: 14,
    color: colors.text.muted,
    marginLeft: 32 + spacing.sm, // align with title (icon width + margin)
  },

  // Team Headers
  teamHeaderRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: spacing.sm,
  },
  teamHeaderLeft: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  teamHeaderRight: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
  },
  teamHeaderText: {
    fontSize: 11,
    fontWeight: '700',
    color: colors.text.muted,
    letterSpacing: 1,
  },
  blackCircle: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: '#1a1a1a', // Literal team jersey color — no theme token
    borderWidth: 1,
    borderColor: colors.border.muted,
  },
  whiteCircle: {
    width: 10,
    height: 10,
    borderRadius: 5,
    backgroundColor: colors.text.primary, // Literal team jersey color (white)
  },

  // Goalie Section
  goalieSection: {
    marginBottom: spacing.xs,
  },

  // Separator
  separator: {
    height: 1,
    backgroundColor: colors.border.default,
    marginBottom: spacing.xs,
  },

  // Skaters Section
  skatersSection: {},

  // Player Row — fixed height ensures consistent card sizing for image capture
  playerRow: {
    flexDirection: 'row',
    height: 30,
    alignItems: 'center',
  },
  leftColumn: {
    flex: 1,
    flexDirection: 'row',
    justifyContent: 'flex-end',
    alignItems: 'center',
    paddingRight: spacing.md,
  },
  rightColumn: {
    flex: 1,
    flexDirection: 'row',
    justifyContent: 'flex-start',
    alignItems: 'center',
    paddingLeft: spacing.md,
  },
  positionLabel: {
    fontSize: 13,
    color: colors.text.muted,
  },
  playerName: {
    fontSize: 13,
    color: colors.text.primary,
  },

  // Footer
  footer: {
    marginTop: spacing.md,
    alignItems: 'center',
  },
  footerBrand: {
    fontSize: 13,
    color: colors.text.muted,
    marginBottom: spacing.xs / 2, // tighter than xs for compact footer
  },
  footerSubtext: {
    fontSize: 11,
    color: colors.text.subtle,
  },
});
