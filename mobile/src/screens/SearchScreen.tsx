import { useState } from "react";
import { Pressable, Text, TextInput, View } from "react-native";
import { useQuery } from "@tanstack/react-query";
import { searchGateway, type SearchResult } from "../api/search";
import { useSessionStore } from "../store/session";
import { Screen } from "../ui/Screen";
import { Header } from "../ui/Header";
import { Card } from "../ui/Card";
import { Pill } from "../ui/Pill";
import { Eyebrow } from "../ui/Eyebrow";
import { colors, radii, typography } from "../theme/tokens";
import { formatClock } from "../lib/time";

export function SearchScreen() {
  const token = useSessionStore((s) => s.accessToken);
  const [draft, setDraft] = useState("");
  const [submitted, setSubmitted] = useState("");
  const [selectedIndex, setSelectedIndex] = useState(0);

  const search = useQuery({
    queryKey: ["search", submitted],
    queryFn: () => searchGateway.query(token!, submitted),
    enabled: Boolean(token) && submitted.trim().length > 0,
    staleTime: 60_000,
  });

  const results = search.data ?? [];
  const selected = results[selectedIndex] ?? results[0] ?? null;

  return (
    <Screen>
      <Header
        subtitle="Поиск"
        title={
          <Text
            style={{
              ...typography.display,
              fontFamily: "Manrope_800ExtraBold",
              fontSize: 24,
              color: colors.bone,
              letterSpacing: -0.6,
            }}
          >
            Где это обсуждали?
          </Text>
        }
      />
      <View style={{ paddingHorizontal: 16, gap: 14 }}>
        <View
          style={{
            height: 54,
            borderRadius: radii.md,
            borderWidth: 1,
            borderColor: colors.borderSoft,
            backgroundColor: colors.surfaceMid,
            paddingHorizontal: 14,
            justifyContent: "center",
          }}
        >
          <TextInput
            value={draft}
            onChangeText={setDraft}
            onSubmitEditing={() => {
              setSubmitted(draft.trim());
              setSelectedIndex(0);
            }}
            returnKeyType="search"
            placeholder="Например: договор, цена, Марина, Friday…"
            placeholderTextColor={colors.ash500}
            style={{ ...typography.bodyMd, fontSize: 15, color: colors.bone }}
          />
        </View>

        {submitted.trim().length === 0 ? (
          <Card>
            <Eyebrow color={colors.ash500}>Подсказка</Eyebrow>
            <Text
              style={{
                ...typography.heading,
                fontFamily: "Manrope_700Bold",
                fontSize: 16,
                color: colors.bone,
                marginTop: 8,
                letterSpacing: -0.3,
              }}
            >
              Введите запрос
            </Text>
            <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
              Можно искать по имени, теме разговора или ключевой фразе.
            </Text>
          </Card>
        ) : null}

        {search.isLoading ? <Card><Text style={{ ...typography.body, color: colors.ash400 }}>Ищем…</Text></Card> : null}
        {search.isError ? (
          <Card>
            <Text style={{ ...typography.body, color: colors.error }}>Не удалось выполнить поиск.</Text>
          </Card>
        ) : null}
        {search.isSuccess && results.length === 0 ? (
          <Card>
            <Text style={{ ...typography.heading, fontFamily: "Manrope_700Bold", fontSize: 16, color: colors.bone, letterSpacing: -0.3 }}>
              Ничего не найдено
            </Text>
            <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 6 }}>
              Попробуйте короче — по имени, теме или паре слов.
            </Text>
          </Card>
        ) : null}

        {selected ? <DetailCard selected={selected} /> : null}

        {results.map((r, i) => (
          <ResultRow
            key={`${r.title}-${r.observedAt}-${i}`}
            result={r}
            selected={i === selectedIndex}
            onPress={() => setSelectedIndex(i)}
          />
        ))}
      </View>
    </Screen>
  );
}

function ResultRow({
  result,
  selected,
  onPress,
}: {
  result: SearchResult;
  selected: boolean;
  onPress: () => void;
}) {
  return (
    <Pressable onPress={onPress}>
      <View
        style={{
          padding: 16,
          borderRadius: radii.lg,
          borderWidth: 1,
          borderColor: selected ? "rgba(229,56,59,0.55)" : colors.borderSoft,
          backgroundColor: colors.ink850,
        }}
      >
        <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 10 }}>
          <Text
            style={{
              ...typography.heading,
              fontFamily: "Manrope_700Bold",
              fontSize: 15,
              color: colors.bone,
              letterSpacing: -0.3,
              flex: 1,
            }}
          >
            {result.title}
          </Text>
          <Text style={{ ...typography.mono, fontSize: 10, color: colors.ash500, letterSpacing: 0.8 }}>
            {formatClock(result.observedAt)}
          </Text>
        </View>
        <Text style={{ ...typography.body, fontSize: 12, color: colors.ash400, marginTop: 6, lineHeight: 17 }}>
          {result.summary}
        </Text>
        <View style={{ flexDirection: "row", gap: 6, marginTop: 8, flexWrap: "wrap" }}>
          <Pill kind="kind">{result.kind}</Pill>
          <Pill kind="neutral">{result.sourceRoom}</Pill>
        </View>
      </View>
    </Pressable>
  );
}

function DetailCard({ selected }: { selected: SearchResult }) {
  return (
    <Card accent>
      <Eyebrow>Контекст</Eyebrow>
      <Text
        style={{
          ...typography.heading,
          fontFamily: "Manrope_700Bold",
          fontSize: 18,
          color: colors.bone,
          marginTop: 8,
          letterSpacing: -0.4,
        }}
      >
        {selected.title}
      </Text>
      <View style={{ flexDirection: "row", gap: 6, marginTop: 8, flexWrap: "wrap" }}>
        <Pill kind="kind">{selected.kind}</Pill>
        <Pill kind="neutral">{selected.sourceRoom}</Pill>
      </View>
      <Text style={{ ...typography.body, fontSize: 13, color: colors.ash400, marginTop: 12, lineHeight: 19 }}>
        {selected.summary}
      </Text>
      {selected.resolutionNote ? (
        <Text style={{ ...typography.body, fontSize: 12, color: colors.ash500, marginTop: 8 }}>
          Итог: {selected.resolutionNote}
        </Text>
      ) : null}
    </Card>
  );
}
